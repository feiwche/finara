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
public class CustomerContextTools(FinaraDbContext dbContext, ICurrentUserContext currentUser)
{
    public async Task<string> GetCustomerProfile(Guid customerId)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return "Müşteri bulunamadı.";

        var segment = await dbContext.Segments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive);

        var sb = new StringBuilder();
        sb.AppendLine($"Ad Soyad: {customer.FirstName} {customer.LastName}");
        sb.AppendLine($"Yaş: {customer.GetAge()}");
        sb.AppendLine($"Yatırım hesabı: {(customer.HasInvestmentAccount ? "var" : "yok")}");
        sb.AppendLine($"Yurtdışı transfer (SWIFT): {(customer.HasInternationalTransfers ? "var" : "yok")}");

        if (segment is null)
        {
            sb.AppendLine("Segment: henüz hesaplanmamış (önce segmentasyon çalıştırılmalı).");
        }
        else
        {
            sb.AppendLine($"Harcama eğilimi: {segment.Propensity}");
            sb.AppendLine($"Baskın harcama kategorisi: {segment.DominantCategory?.ToString() ?? "belirsiz"}");
            sb.AppendLine($"RFM skoru: Recency={segment.RecencyScore} Frequency={segment.FrequencyScore} Monetary={segment.MonetaryScore} (toplam {segment.GetTotalScore()}/15)");
        }

        return sb.ToString();
    }

    public async Task<string> GetCustomerTransactions(Guid customerId, int days = 90)
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
            return $"Son {days} günde işlem bulunamadı.";

        var sb = new StringBuilder();
        sb.AppendLine($"Son {days} gündeki işlemler (en yeni {transactions.Count} kayıt):");
        foreach (var t in transactions)
            sb.AppendLine($"- {t.OccurredAtUtc:yyyy-MM-dd} | {t.Category} | {t.MerchantName} | {t.Amount} {t.Currency}");

        return sb.ToString();
    }

    public async Task<string> GetCustomerOffers(Guid customerId)
    {
        var offers = await dbContext.CampaignOffers.AsNoTracking()
            .Include(o => o.Campaign)
            .Where(o => o.CustomerId == customerId && (currentUser.IsSuperAdmin || o.BankId == currentUser.BankId))
            .OrderByDescending(o => o.PresentedAtUtc)
            .ToListAsync();

        if (offers.Count == 0)
            return "Bu müşteriye henüz hiç kampanya teklifi sunulmamış.";

        var sb = new StringBuilder();
        sb.AppendLine($"Sunulan teklifler ({offers.Count} adet):");
        foreach (var o in offers)
            sb.AppendLine($"- {o.Campaign?.Name} | güven skoru: {o.ConfidenceScore:0} | durum: {o.Response}");

        return sb.ToString();
    }

    public async Task<string> GetActiveCampaigns()
    {
        var campaigns = await dbContext.Campaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Active && (currentUser.IsSuperAdmin || c.BankId == currentUser.BankId))
            .ToListAsync();

        if (campaigns.Count == 0)
            return "Şu anda aktif kampanya yok.";

        var sb = new StringBuilder();
        sb.AppendLine($"Aktif kampanyalar ({campaigns.Count} adet):");
        foreach (var c in campaigns)
            sb.AppendLine($"- {c.Name}: {c.Description} (hedef kategori: {c.TargetCategory?.ToString() ?? "yok"}, hedef eğilim: {c.TargetPropensity?.ToString() ?? "yok"})");

        return sb.ToString();
    }
}
