using Finara.Application.Contracts;
using Finara.Domain.Enums;

namespace Finara.Application.Interfaces;

// INotificationDispatcher (Customer + IConsentGate + NotificationLog etrafında kurulu)
// kasıtlı olarak reuse edilmedi — bu, banka müşterisine değil, sistemi kullanan bir
// yönetici (User) hesabına giden işlemsel bir e-posta; rıza kontrolü kavramsal olarak
// uygulanmıyor. Ayrı ve dar bir arayüz.
//
// `language`: e-postanın hangi dilde gönderileceği — alıcının kendi User.Preferences.Language'i
// (şifre sıfırlama/haftalık özet) ya da daveti gönderen kişininki (davet — daveti alan henüz
// bir User satırı olmadığı için kendi tercihi yok). Bilinçli olarak müşteriye (bankanın kendi
// müşterisine) giden kampanya/teklif bildirimleriyle KARIŞTIRILMADI — o tamamen ayrı bir
// kavram (INotificationDispatcher), bankanın kendi müşteri iletişim diliyle ilgili, burada
// ele alınmıyor.
public interface IAdminEmailSender
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, UiLanguage language, CancellationToken cancellationToken = default);

    Task SendInvitationEmailAsync(string toEmail, string acceptUrl, UiLanguage language, CancellationToken cancellationToken = default);

    Task SendWeeklyDigestEmailAsync(
        string toEmail, string bankName, WeeklyDigestStats stats, DateTime weekStartUtc, DateTime weekEndUtc, UiLanguage language,
        CancellationToken cancellationToken = default);

    // Arka plan otomasyonları gecikince AutomationWatchdogService tarafından SuperAdmin'lere
    // gönderilir. /health/automation ucu zaten vardı ama onu yoklayan bir şey yoktu —
    // otomasyon sessizce dursa kimse fark etmiyordu (denetimde bulundu).
    Task SendAutomationAlertEmailAsync(
        string toEmail, IReadOnlyList<AutomationAlertJob> jobs, UiLanguage language, CancellationToken cancellationToken = default);
}
