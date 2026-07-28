using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Finara.Infrastructure.Services;

// MockEmailDispatcher'ın admin/auth e-postaları için karşılığı — gerçekten göndermiyor,
// dev ortamında sıfırlama bağlantısını doğrulayabilmek için bilgi seviyesinde logluyor
// (bu akışın NotificationLog gibi DB'de görüntülenebilir bir ekranı yok).
public class MockAdminEmailSender(ILogger<MockAdminEmailSender> logger) : IAdminEmailSender
{
    public Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, UiLanguage language, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Mock] Şifre sıfırlama e-postası ({Language}) — Alıcı: {Email}, Bağlantı: {ResetUrl}", language, toEmail, resetUrl);
        return Task.CompletedTask;
    }

    public Task SendInvitationEmailAsync(string toEmail, string acceptUrl, UiLanguage language, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Mock] Davet e-postası ({Language}) — Alıcı: {Email}, Bağlantı: {AcceptUrl}", language, toEmail, acceptUrl);
        return Task.CompletedTask;
    }

    public Task SendWeeklyDigestEmailAsync(
        string toEmail, string bankName, WeeklyDigestStats stats, DateTime weekStartUtc, DateTime weekEndUtc, UiLanguage language,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Mock] Haftalık özet e-postası ({Language}) — Alıcı: {Email}, Banka: {BankName}, Hafta: {WeekStart:dd.MM}-{WeekEnd:dd.MM}, " +
            "Yeni müşteri: {NewCustomers}, Kabul: {Accepted}, Red: {Declined}, Aktif kampanya: {ActiveCampaigns}",
            language, toEmail, bankName, weekStartUtc, weekEndUtc, stats.NewCustomers, stats.OffersAccepted, stats.OffersDeclined, stats.ActiveCampaigns);
        return Task.CompletedTask;
    }
}
