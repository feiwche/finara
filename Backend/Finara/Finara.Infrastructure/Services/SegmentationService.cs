using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Domain.Services;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace Finara.Infrastructure.Services;

public class SegmentationService(
    FinaraDbContext dbContext,
    IAuditLogRepository auditLogRepository,
    ICurrentUserContext currentUser,
    IChatClient chatClient,
    CurrencyConverter currencyConverter)
    : ISegmentationService
{
    // RFM ve baskın kategori hesaplaması bu pencereyle sınırlı — çok eski işlemler
    // güncel davranışı yansıtmadığı için segmentasyona dahil edilmiyor.
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(90);

    public async Task<RecalculateSegmentsResult> RecalculateAllAsync(CancellationToken cancellationToken = default)
    {
        var referenceUtc = DateTime.UtcNow;
        var lookbackStart = referenceUtc - LookbackWindow;

        // SuperAdmin tüm bankalar için yeniden hesaplar, BankAdmin sadece kendi bankası için.
        var transactionsQuery = dbContext.Transactions.AsQueryable();
        // KVKK unutulma hakkıyla anonimleştirilmiş (IsErased) müşteriler yeniden
        // profillenmiyor — aksi halde geçmiş (silinmeyen) işlemleri üzerinden RFM'leri
        // tekrar hesaplanıp CurrentPropensity güncellenir, yeni bir Segment/AuditLog
        // (SegmentAssigned) kaydı oluşurdu; bu, "unutulma" niyetiyle doğrudan çelişirdi.
        var customersQuery = dbContext.Customers.Where(c => !c.IsErased).AsQueryable();
        var segmentsQuery = dbContext.Segments.AsQueryable();
        if (!currentUser.IsSuperAdmin)
        {
            transactionsQuery = transactionsQuery.Where(t => t.BankId == currentUser.BankId);
            customersQuery = customersQuery.Where(c => c.BankId == currentUser.BankId);
            segmentsQuery = segmentsQuery.Where(s => s.BankId == currentUser.BankId);
        }

        // BankId de gruplamaya dahil — RFM quintile skorlaması (aşağıda banka bazlı ayrı ayrı
        // hesaplanıyor, bkz. o bölümdeki yorum) hangi bankaya ait olduğunu bilmek zorunda.
        // Currency de gruplamaya dahil — farklı para birimlerindeki tutarlar VERİTABANINDA
        // toplanamaz (1 USD = 1 TRY olurdu, denetimde bulundu). Her para birimi ayrı toplanıp
        // aşağıda temel para birimine çevrilerek birleştiriliyor.
        var currencyTotals = await transactionsQuery
            .Where(t => t.OccurredAtUtc >= lookbackStart)
            .GroupBy(t => new { t.CustomerId, t.BankId, t.Currency })
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.BankId,
                g.Key.Currency,
                LastTransactionUtc = g.Max(t => t.OccurredAtUtc),
                Count = g.Count(),
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync(cancellationToken);

        var activitySummaries = currencyTotals
            .GroupBy(x => new { x.CustomerId, x.BankId })
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.BankId,
                LastTransactionUtc = g.Max(x => x.LastTransactionUtc),
                Count = g.Sum(x => x.Count),
                Total = g.Sum(x => currencyConverter.ToBase(x.Total, x.Currency))
            })
            .ToList();

        var totalCustomers = await customersQuery.CountAsync(cancellationToken);
        var activeCustomerIds = activitySummaries.Select(a => a.CustomerId).ToHashSet();

        // Aktivite penceresinde artık görünmeyen ama önceden aktif segmenti olan müşteriler —
        // bunların segmenti deaktive edilmezse sonsuza dek "güncel"miş gibi görünür kalır.
        // Faz 4 denetiminde bulundu: işlem 90 günün dışına çıktığında segment hiç değişmiyordu.
        var staleActiveSegments = await segmentsQuery
            .Where(s => s.IsActive && !activeCustomerIds.Contains(s.CustomerId))
            .ToListAsync(cancellationToken);
        if (staleActiveSegments.Count > 0)
        {
            // Segment deaktive olunca müşterinin CurrentPropensity'si de Nötr'e dönmeli —
            // yoksa segment yokken (404) listede eski bir eğilim gösterilmeye devam eder.
            var staleCustomerIds = staleActiveSegments.Select(s => s.CustomerId).ToList();
            var staleCustomers = await customersQuery
                .Where(c => staleCustomerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            foreach (var stale in staleActiveSegments)
            {
                stale.Deactivate();
                if (staleCustomers.TryGetValue(stale.CustomerId, out var staleCustomer))
                    staleCustomer.AssignPropensity(SpendingPropensity.Neutral);
            }
        }

        if (activitySummaries.Count > 0)
        {
            // KRİTİK: quintile skorlaması (RfmCalculator.CalculateScores) popülasyona GÖRECELİ —
            // "bu müşteri diğerlerine göre ne kadar aktif" sorusuna cevap veriyor. Otomatik
            // döngü SuperAdmin kimliğiyle (SystemCurrentUserContext) çalıştığı için banka
            // filtresi hiç uygulanmıyordu — bulundu: TÜM BANKALARIN müşterileri TEK bir
            // popülasyonda karışıyordu, yani bir bankanın müşterisinin "harcamaya yatkın mı"
            // skoru o an sistemde kayıtlı BAŞKA bir bankanın müşteri hacmine göre şekilleniyordu
            // (çok-kiracılı bir sistemde kabul edilemez bir izolasyon ihlali — VakıfBank gibi
            // ikinci bir gerçek banka eklenince yanlış/tutarsız segmentasyona yol açardı). Şimdi
            // her banka KENDİ popülasyonu içinde ayrı ayrı skorlanıp sonuçlar birleştiriliyor.
            var rfmScores = new Dictionary<Guid, RfmCalculator.RfmScore>();
            foreach (var bankGroup in activitySummaries.GroupBy(a => a.BankId))
            {
                var bankRfmInputs = bankGroup
                    .Select(a => new RfmCalculator.CustomerActivity(a.CustomerId, a.LastTransactionUtc, a.Count, a.Total))
                    .ToList();
                foreach (var score in RfmCalculator.CalculateScores(bankRfmInputs, referenceUtc))
                    rfmScores[score.CustomerId] = score;
            }

            // Baskın kategori: pencere içinde en çok harcanan kategori (tutar bazlı).
            // Burada da Currency gruplamaya dahil — aksi halde USD'li bir kategori, TRY'li bir
            // kategoriyle haksız biçimde karşılaştırılıp yanlış "baskın" kategori seçilirdi.
            var categoryTotals = await transactionsQuery
                .Where(t => t.OccurredAtUtc >= lookbackStart)
                .GroupBy(t => new { t.CustomerId, t.Category, t.Currency })
                .Select(g => new { g.Key.CustomerId, g.Key.Category, g.Key.Currency, Total = g.Sum(t => t.Amount) })
                .ToListAsync(cancellationToken);

            var dominantCategories = categoryTotals
                .GroupBy(x => new { x.CustomerId, x.Category })
                .Select(g => new
                {
                    g.Key.CustomerId,
                    g.Key.Category,
                    Total = g.Sum(x => currencyConverter.ToBase(x.Total, x.Currency))
                })
                .GroupBy(x => x.CustomerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Total).First().Category);

            var customerIds = rfmScores.Keys.ToList();
            var existingActiveSegments = await dbContext.Segments
                .Where(s => customerIds.Contains(s.CustomerId) && s.IsActive)
                .ToListAsync(cancellationToken);
            var existingActiveByCustomer = existingActiveSegments.ToDictionary(s => s.CustomerId);

            // Segment.Propensity (yeni segment kaydı) tek doğruluk kaynağı olsa da,
            // Customer.CurrentPropensity (CustomerSummary.propensity ile dışarı veriliyor —
            // müşteri listesi/Dashboard bunu okuyor) senkronize edilmezse sonsuza dek
            // varsayılan Neutral'da kalır. Bulundu ve düzeltildi: bu satır hiç yoktu.
            var customers = await customersQuery
                .Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            foreach (var customerId in customerIds)
            {
                // Müşteri bu banka scope'unda değilse (BankAdmin başka bir bankanın işlemini
                // hiç görmez zaten, ama savunma amaçlı) sessizce atlanır.
                if (!customers.TryGetValue(customerId, out var customer))
                    continue;

                var rfm = rfmScores[customerId];
                var propensity = RfmCalculator.DeterminePropensity(rfm.TotalScore);
                var dominantCategory = dominantCategories.GetValueOrDefault(customerId);

                if (existingActiveByCustomer.TryGetValue(customerId, out var existing))
                    existing.Deactivate();

                // customer.BankId kullanılıyor (currentUser.ResolveBankIdForWrite() değil) —
                // SuperAdmin birden fazla bankanın müşterilerini tek seferde yeniden hesaplarken
                // her segmentin, ait olduğu müşterinin GERÇEK bankasına damgalanması gerekir.
                dbContext.Segments.Add(Segment.Create(
                    customer.BankId, customerId, rfm.RecencyScore, rfm.FrequencyScore, rfm.MonetaryScore, propensity, dominantCategory));

                customer.AssignPropensity(propensity);

                // currentUser.UserId null ise bu çağrı SystemCurrentUserContext (otomasyon)
                // üzerinden geliyor demektir — bu servise (manuel "Yeniden Hesapla" hariç)
                // başka hiçbir null-UserId'li çağıran yok. Denetim Kaydı ekranında otomasyon
                // ile manuel işlemleri ayırt edilebilir kılmak için [Otomasyon] etiketleniyor
                // (SystemCurrentUserContext'teki yorum bunu vaat ediyordu ama kod hiç
                // yapmıyordu — denetimde bulundu).
                var details = $"{propensity} / baskın kategori: {dominantCategory}";
                if (currentUser.UserId is null)
                    details = $"[Otomasyon] {details}";

                await auditLogRepository.InsertAsync(
                    AuditLog.Create(customer.BankId, customerId, AuditActionType.SegmentAssigned, details, currentUser.UserId),
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RecalculateSegmentsResult(activeCustomerIds.Count, totalCustomers - activeCustomerIds.Count);
    }

    public async Task<SegmentSummary?> GetActiveSegmentAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var segment = await dbContext.Segments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive, cancellationToken);

        return segment is null || (!currentUser.IsSuperAdmin && segment.BankId != currentUser.BankId)
            ? null
            : ToSummary(segment);
    }

    public async Task<IReadOnlyList<SegmentSummary>?> GetSegmentHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        var history = await dbContext.Segments
            .AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .OrderBy(s => s.CalculatedAtUtc)
            .ToListAsync(cancellationToken);

        return history.Select(ToSummary).ToList();
    }

    public async Task<SegmentSummary?> GenerateAiCommentaryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var segment = await dbContext.Segments.FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive, cancellationToken);
        if (segment is null || (!currentUser.IsSuperAdmin && segment.BankId != currentUser.BankId))
            return null;

        // Tool-calling gerekmiyor — ihtiyaç duyulan tüm veri (RFM skorları, eğilim, baskın
        // kategori) zaten elimizde; CustomerContextTools'un aksine burada LLM'e ekstra bir
        // sorgu turu yaptırmıyoruz, tek bir düz metin tamamlama yeterli.
        var prompt =
            $"""
            Bir bankanın müşteri segmentasyon sistemisin. Sana RFM (Recency/Frequency/Monetary)
            analiziyle hesaplanmış bir müşteri segmenti veriliyor. Bu segmenti 2-3 cümleyle, bir
            banka çalışanının hızlıca okuyup anlayacağı şekilde YORUMLA — sayısal skorları
            tekrar etme, ne anlama geldiğini açıkla. Kısa bir aksiyon önerisi ekleyebilirsin ama
            bunu kesin bir karar gibi sunma; nihai karar her zaman RFM kuralına ait. Türkçe yanıt ver.

            Recency skoru: {segment.RecencyScore}/5
            Frequency skoru: {segment.FrequencyScore}/5
            Monetary skoru: {segment.MonetaryScore}/5
            Toplam skor: {segment.GetTotalScore()}/15
            Eğilim: {segment.Propensity}
            Baskın harcama kategorisi: {segment.DominantCategory?.ToString() ?? "yok"}
            """;

        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        segment.SetAiCommentary(response.Text);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(segment);
    }

    private static SegmentSummary ToSummary(Segment segment) => new(
        segment.CustomerId, segment.RecencyScore, segment.FrequencyScore, segment.MonetaryScore,
        segment.GetTotalScore(), segment.Propensity, segment.DominantCategory, segment.CalculatedAtUtc,
        segment.AiCommentary);
}
