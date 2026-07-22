namespace Finara.Application.Interfaces;

// INotificationDispatcher (Customer + IConsentGate + NotificationLog etrafında kurulu)
// kasıtlı olarak reuse edilmedi — bu, banka müşterisine değil, sistemi kullanan bir
// yönetici (User) hesabına giden işlemsel bir e-posta; rıza kontrolü kavramsal olarak
// uygulanmıyor. Ayrı ve dar bir arayüz.
public interface IAdminEmailSender
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken cancellationToken = default);
}
