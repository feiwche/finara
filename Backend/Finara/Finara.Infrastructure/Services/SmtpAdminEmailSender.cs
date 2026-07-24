using System.Net;
using System.Net.Mail;
using Finara.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Finara.Infrastructure.Services;

// SmtpEmailDispatcher'ın admin/auth karşılığı — aynı SmtpOptions'ı paylaşıyor ama
// IConsentGate/NotificationLog yok (bkz. IAdminEmailSender XML yorumu).
public class SmtpAdminEmailSender(IOptions<SmtpOptions> options) : IAdminEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };
        using var mail = new MailMessage(
            _options.FromAddress,
            toEmail,
            "Finara — Şifre Sıfırlama",
            $"Şifrenizi sıfırlamak için bağlantıya tıklayın (30 dakika geçerlidir):\n\n{resetUrl}\n\nBu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.");

        await client.SendMailAsync(mail, cancellationToken);
    }

    public async Task SendInvitationEmailAsync(string toEmail, string acceptUrl, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };
        using var mail = new MailMessage(
            _options.FromAddress,
            toEmail,
            "Finara — Hesap Daveti",
            $"Finara'ya davet edildiniz. Hesabınızı oluşturmak için bağlantıya tıklayın (24 saat geçerlidir):\n\n{acceptUrl}\n\nBu daveti siz talep etmediyseniz bu e-postayı yok sayabilirsiniz.");

        await client.SendMailAsync(mail, cancellationToken);
    }
}
