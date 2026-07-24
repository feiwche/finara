using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Finara.Infrastructure.Services;

// MockEmailDispatcher'ın admin/auth e-postaları için karşılığı — gerçekten göndermiyor,
// dev ortamında sıfırlama bağlantısını doğrulayabilmek için bilgi seviyesinde logluyor
// (bu akışın NotificationLog gibi DB'de görüntülenebilir bir ekranı yok).
public class MockAdminEmailSender(ILogger<MockAdminEmailSender> logger) : IAdminEmailSender
{
    public Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Mock] Şifre sıfırlama e-postası — Alıcı: {Email}, Bağlantı: {ResetUrl}", toEmail, resetUrl);
        return Task.CompletedTask;
    }

    public Task SendInvitationEmailAsync(string toEmail, string acceptUrl, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Mock] Davet e-postası — Alıcı: {Email}, Bağlantı: {AcceptUrl}", toEmail, acceptUrl);
        return Task.CompletedTask;
    }

    public Task SendWeeklyDigestEmailAsync(
        string toEmail, string bankName, WeeklyDigestStats stats, DateTime weekStartUtc, DateTime weekEndUtc,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Mock] Haftalık özet e-postası — Alıcı: {Email}, Banka: {BankName}, Hafta: {WeekStart:dd.MM}-{WeekEnd:dd.MM}, " +
            "Yeni müşteri: {NewCustomers}, Kabul: {Accepted}, Red: {Declined}, Aktif kampanya: {ActiveCampaigns}",
            toEmail, bankName, weekStartUtc, weekEndUtc, stats.NewCustomers, stats.OffersAccepted, stats.OffersDeclined, stats.ActiveCampaigns);
        return Task.CompletedTask;
    }
}
