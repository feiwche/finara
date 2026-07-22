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
}
