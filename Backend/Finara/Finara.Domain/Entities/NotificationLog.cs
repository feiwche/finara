using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// Faz 8.4 — INotificationDispatcher'ın her dağıtım denemesinin (başarılı ya da rıza
// eksikliği yüzünden atlanmış) kaydı. AuditLog'daki OfferDistributed girdisiyle birlikte
// çalışır ama daha detaylı (mesaj içeriği, atlama nedeni) — BackOffice'in Bildirim Kaydı
// ekranı bunu okur.
public class NotificationLog : BaseEntity
{
    public Guid BankId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? CampaignOfferId { get; private set; }
    public ConsentChannel Channel { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public bool Sent { get; private set; }
    public string? SkippedReason { get; private set; }
    public DateTime SentAtUtc { get; private set; }

    private NotificationLog() { }

    public static NotificationLog Create(
        Guid bankId,
        Guid customerId,
        ConsentChannel channel,
        string message,
        bool sent,
        string? skippedReason = null,
        Guid? campaignOfferId = null)
    {
        if (bankId == Guid.Empty)
            throw new ArgumentException("Geçerli bir banka kimliği gerekli.", nameof(bankId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Bildirim mesajı boş olamaz.", nameof(message));

        if (!sent && string.IsNullOrWhiteSpace(skippedReason))
            throw new ArgumentException("Gönderilmeyen bir bildirim için atlama nedeni belirtilmeli.", nameof(skippedReason));

        return new NotificationLog
        {
            BankId = bankId,
            CustomerId = customerId,
            CampaignOfferId = campaignOfferId,
            Channel = channel,
            Message = message,
            Sent = sent,
            SkippedReason = sent ? null : skippedReason,
            SentAtUtc = DateTime.UtcNow
        };
    }
}
