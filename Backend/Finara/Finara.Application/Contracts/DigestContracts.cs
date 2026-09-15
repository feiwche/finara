namespace Finara.Application.Contracts;

// WeeklyDigestService'in her BankAdmin'e gönderdiği haftalık özet e-postasının içeriği —
// son 7 günün AuditLogs'tan hesaplanmış özeti + anlık (o ana ait) toplam sayılar.
public record WeeklyDigestStats(
    int TotalCustomers,
    int ActiveCampaigns,
    int NewCustomers,
    int OffersAccepted,
    int OffersDeclined);

// AutomationWatchdogService'in gecikmiş bir otomasyon için gönderdiği alarm e-postasında
// her bir iş satırı. LastSuccessUtc null ise o iş uygulama başladığından beri hiç başarıyla
// tamamlanmamış demektir.
public record AutomationAlertJob(string JobName, DateTime? LastSuccessUtc);
