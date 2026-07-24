namespace Finara.Application.Contracts;

// WeeklyDigestService'in her BankAdmin'e gönderdiği haftalık özet e-postasının içeriği —
// son 7 günün AuditLogs'tan hesaplanmış özeti + anlık (o ana ait) toplam sayılar.
public record WeeklyDigestStats(
    int TotalCustomers,
    int ActiveCampaigns,
    int NewCustomers,
    int OffersAccepted,
    int OffersDeclined);
