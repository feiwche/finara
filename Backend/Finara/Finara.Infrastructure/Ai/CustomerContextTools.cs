using System.Text;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Ai;

// Modele sunulan müşteri context'i — hem MCP tool olarak (bkz. CustomerMcpTools) hem
// de kendi ai-insight endpoint'imizde native function-calling olarak kullanılıyor,
// tek bir yerden. Çıktılar bilerek düz metin: LLM'ler serbest metni JSON'dan daha
// güvenilir yorumluyor ve tool şemasını basit tutuyor.
//
// Faz 8.1: bank-scope'lanmadan önce bir BankAdmin, customerId'sini bilse/tahmin
// etse başka bir bankanın müşteri verisini AI/MCP üzerinden çekebiliyordu — bulundu
// ve düzeltildi, her metot artık ICurrentUserContext ile banka sınırını kontrol ediyor.
//
// DİL: context'in tamamı (alan adları + enum değerleri) kullanıcının o an kullandığı
// arayüz diline göre üretiliyor. Öncesinde context sabit Türkçe alan adları ve HAM
// İngilizce enum değerleri içeriyordu; model bunları yanıtına olduğu gibi kopyalayıp
// "Harcama profili HighlyResponsive'dır" gibi karışık dilli cevaplar üretiyordu (canlı
// testte bulundu). MCP yolu dil parametresi göndermediği için varsayılan Türkçe'de kalır.
public class CustomerContextTools(FinaraDbContext dbContext, ICurrentUserContext currentUser)
{
    public async Task<string> GetCustomerProfile(Guid customerId, UiLanguage language = UiLanguage.Turkish)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return language switch
            {
                UiLanguage.English => "Customer not found.",
                UiLanguage.German => "Kunde nicht gefunden.",
                _ => "Müşteri bulunamadı."
            };

        var segment = await dbContext.Segments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive);

        var (fullName, age, investment, swift, yes, no, noSegment, propensityLabel, categoryLabel, unknown, rfm) = language switch
        {
            UiLanguage.English => ("Full name", "Age", "Investment account", "International transfer (SWIFT)", "yes", "no",
                "Segment: not calculated yet (segmentation must be run first).", "Spending propensity", "Dominant spending category", "unknown", "RFM score"),
            UiLanguage.German => ("Name", "Alter", "Anlagekonto", "Auslandsüberweisung (SWIFT)", "ja", "nein",
                "Segment: noch nicht berechnet (Segmentierung muss zuerst ausgeführt werden).", "Ausgabeneigung", "Dominante Ausgabenkategorie", "unbekannt", "RFM-Wert"),
            _ => ("Ad Soyad", "Yaş", "Yatırım hesabı", "Yurtdışı transfer (SWIFT)", "var", "yok",
                "Segment: henüz hesaplanmamış (önce segmentasyon çalıştırılmalı).", "Harcama eğilimi", "Baskın harcama kategorisi", "belirsiz", "RFM skoru")
        };

        var sb = new StringBuilder();
        sb.AppendLine($"{fullName}: {customer.FirstName} {customer.LastName}");
        sb.AppendLine($"{age}: {customer.GetAge()}");
        sb.AppendLine($"{investment}: {(customer.HasInvestmentAccount ? yes : no)}");
        sb.AppendLine($"{swift}: {(customer.HasInternationalTransfers ? yes : no)}");

        if (segment is null)
        {
            sb.AppendLine(noSegment);
        }
        else
        {
            sb.AppendLine($"{propensityLabel}: {AiContextLabels.Label(segment.Propensity, language)}");
            sb.AppendLine($"{categoryLabel}: {(segment.DominantCategory is { } dc ? AiContextLabels.Label(dc, language) : unknown)}");
            sb.AppendLine($"{rfm}: Recency={segment.RecencyScore} Frequency={segment.FrequencyScore} Monetary={segment.MonetaryScore} ({segment.GetTotalScore()}/15)");
        }

        return sb.ToString();
    }

    public async Task<string> GetCustomerTransactions(Guid customerId, int days = 90, UiLanguage language = UiLanguage.Turkish)
    {
        // LLM (ya da dış bir MCP istemcisi) aşırı büyük/negatif bir değer gönderirse
        // DateTime.UtcNow.AddDays(-days) geçerli tarih aralığının dışına taşıp
        // ArgumentOutOfRangeException fırlatıyordu (denetimde bulundu). 10 yıl
        // makul bir üst sınır.
        days = Math.Clamp(days, 1, 3650);
        var since = DateTime.UtcNow.AddDays(-days);
        var transactions = await dbContext.Transactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId && (currentUser.IsSuperAdmin || t.BankId == currentUser.BankId) && t.OccurredAtUtc >= since)
            .OrderByDescending(t => t.OccurredAtUtc)
            .Take(20)
            .ToListAsync();

        if (transactions.Count == 0)
            return language switch
            {
                UiLanguage.English => $"No transactions found in the last {days} days.",
                UiLanguage.German => $"Keine Transaktionen in den letzten {days} Tagen gefunden.",
                _ => $"Son {days} günde işlem bulunamadı."
            };

        var header = language switch
        {
            UiLanguage.English => $"Transactions in the last {days} days (most recent {transactions.Count} records):",
            UiLanguage.German => $"Transaktionen der letzten {days} Tage (neueste {transactions.Count} Einträge):",
            _ => $"Son {days} gündeki işlemler (en yeni {transactions.Count} kayıt):"
        };

        var sb = new StringBuilder();
        sb.AppendLine(header);
        foreach (var t in transactions)
            sb.AppendLine($"- {t.OccurredAtUtc:yyyy-MM-dd} | {AiContextLabels.Label(t.Category, language)} | {t.MerchantName} | {t.Amount} {t.Currency}");

        return sb.ToString();
    }

    public async Task<string> GetCustomerOffers(Guid customerId, UiLanguage language = UiLanguage.Turkish)
    {
        var offers = await dbContext.CampaignOffers.AsNoTracking()
            .Include(o => o.Campaign)
            .Where(o => o.CustomerId == customerId && (currentUser.IsSuperAdmin || o.BankId == currentUser.BankId))
            .OrderByDescending(o => o.PresentedAtUtc)
            .ToListAsync();

        if (offers.Count == 0)
            return language switch
            {
                UiLanguage.English => "No campaign offers have been presented to this customer yet.",
                UiLanguage.German => "Diesem Kunden wurden noch keine Kampagnenangebote unterbreitet.",
                _ => "Bu müşteriye henüz hiç kampanya teklifi sunulmamış."
            };

        var (header, confidence, status) = language switch
        {
            UiLanguage.English => ($"Presented offers ({offers.Count}):", "confidence score", "status"),
            UiLanguage.German => ($"Unterbreitete Angebote ({offers.Count}):", "Konfidenzwert", "Status"),
            _ => ($"Sunulan teklifler ({offers.Count} adet):", "güven skoru", "durum")
        };

        var sb = new StringBuilder();
        sb.AppendLine(header);
        foreach (var o in offers)
            sb.AppendLine($"- {o.Campaign?.Name} | {confidence}: {o.ConfidenceScore:0} | {status}: {AiContextLabels.Label(o.Response, language)}");

        return sb.ToString();
    }

    public async Task<string> GetActiveCampaigns(UiLanguage language = UiLanguage.Turkish)
    {
        var campaigns = await dbContext.Campaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Active && (currentUser.IsSuperAdmin || c.BankId == currentUser.BankId))
            .ToListAsync();

        if (campaigns.Count == 0)
            return language switch
            {
                UiLanguage.English => "There are no active campaigns right now.",
                UiLanguage.German => "Derzeit gibt es keine aktiven Kampagnen.",
                _ => "Şu anda aktif kampanya yok."
            };

        var (header, targetCategory, targetPropensity, none) = language switch
        {
            UiLanguage.English => ($"Active campaigns ({campaigns.Count}):", "target category", "target propensity", "none"),
            UiLanguage.German => ($"Aktive Kampagnen ({campaigns.Count}):", "Zielkategorie", "Zielneigung", "keine"),
            _ => ($"Aktif kampanyalar ({campaigns.Count} adet):", "hedef kategori", "hedef eğilim", "yok")
        };

        var sb = new StringBuilder();
        sb.AppendLine(header);
        foreach (var c in campaigns)
        {
            var category = c.TargetCategory is { } tc ? AiContextLabels.Label(tc, language) : none;
            var propensities = c.TargetPropensities.Count > 0
                ? string.Join(", ", c.TargetPropensities.Select(p => AiContextLabels.Label(p, language)))
                : none;
            sb.AppendLine($"- {c.Name}: {c.Description} ({targetCategory}: {category}, {targetPropensity}: {propensities})");
        }

        return sb.ToString();
    }
}
