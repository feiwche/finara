using System.Net;
using System.Net.Mail;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Infrastructure.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Finara.Infrastructure.Services;

// SmtpEmailDispatcher'ın admin/auth karşılığı — aynı SmtpOptions'ı paylaşıyor ama
// IConsentGate/NotificationLog yok (bkz. IAdminEmailSender XML yorumu).
public class SmtpAdminEmailSender(IOptions<SmtpOptions> options) : IAdminEmailSender
{
    private readonly SmtpOptions _options = options.Value;
    private static readonly IAsyncPolicy RetryPolicy = Policy.Handle<SmtpException>().WaitAndRetryAsync(TransientRetry.Delays);

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
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(EmailTemplates.Wrap($"""
            <p>Merhaba,</p>
            <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın. Bağlantı <strong>30 dakika</strong> geçerlidir.</p>
            {EmailTemplates.Button(resetUrl, "Şifreyi Sıfırla")}
            <p style="color:#4A5C53;font-size:13px;">Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>
            """), null, "text/html"));

        await RetryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
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
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(EmailTemplates.Wrap($"""
            <p>Merhaba,</p>
            <p>Finara'ya davet edildiniz. Hesabınızı oluşturmak için aşağıdaki bağlantıya tıklayın. Bağlantı <strong>24 saat</strong> geçerlidir.</p>
            {EmailTemplates.Button(acceptUrl, "Daveti Kabul Et")}
            <p style="color:#4A5C53;font-size:13px;">Bu daveti siz talep etmediyseniz bu e-postayı yok sayabilirsiniz.</p>
            """), null, "text/html"));

        await RetryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
    }

    public async Task SendWeeklyDigestEmailAsync(
        string toEmail, string bankName, WeeklyDigestStats stats, DateTime weekStartUtc, DateTime weekEndUtc,
        CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };
        using var mail = new MailMessage(
            _options.FromAddress,
            toEmail,
            $"Finara — {bankName} Haftalık Özet ({weekStartUtc:dd.MM} - {weekEndUtc:dd.MM})",
            $"""
            Merhaba,

            {bankName} için {weekStartUtc:dd.MM.yyyy} - {weekEndUtc:dd.MM.yyyy} haftasının özeti:

            - Yeni müşteri: {stats.NewCustomers}
            - Kabul edilen teklif: {stats.OffersAccepted}
            - Reddedilen teklif: {stats.OffersDeclined}
            - Aktif kampanya: {stats.ActiveCampaigns}
            - Toplam müşteri: {stats.TotalCustomers}

            Detaylar için BackOffice panelindeki Genel Bakış ekranını ziyaret edin.
            """);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(EmailTemplates.Wrap($"""
            <p>Merhaba,</p>
            <p><strong>{bankName}</strong> için {weekStartUtc:dd.MM.yyyy} - {weekEndUtc:dd.MM.yyyy} haftasının özeti:</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse;">
              <tr><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;color:#4A5C53;">Yeni müşteri</td><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;text-align:right;font-weight:600;">{stats.NewCustomers}</td></tr>
              <tr><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;color:#4A5C53;">Kabul edilen teklif</td><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;text-align:right;font-weight:600;">{stats.OffersAccepted}</td></tr>
              <tr><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;color:#4A5C53;">Reddedilen teklif</td><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;text-align:right;font-weight:600;">{stats.OffersDeclined}</td></tr>
              <tr><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;color:#4A5C53;">Aktif kampanya</td><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;text-align:right;font-weight:600;">{stats.ActiveCampaigns}</td></tr>
              <tr><td style="padding:8px 0;color:#4A5C53;">Toplam müşteri</td><td style="padding:8px 0;text-align:right;font-weight:600;">{stats.TotalCustomers}</td></tr>
            </table>
            <p style="color:#4A5C53;font-size:13px;">Detaylar için BackOffice panelindeki Genel Bakış ekranını ziyaret edin.</p>
            """), null, "text/html"));

        await RetryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
    }
}
