using System.Net;
using System.Net.Mail;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;
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

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, UiLanguage language, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };
        using var mail = new MailMessage(
            _options.FromAddress,
            toEmail,
            EmailTemplates.PasswordResetSubject(language),
            EmailTemplates.PasswordResetPlainText(language, resetUrl));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            EmailTemplates.Wrap(EmailTemplates.PasswordResetHtmlBody(language, resetUrl), language), null, "text/html"));

        await RetryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
    }

    public async Task SendInvitationEmailAsync(string toEmail, string acceptUrl, UiLanguage language, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };
        using var mail = new MailMessage(
            _options.FromAddress,
            toEmail,
            EmailTemplates.InvitationSubject(language),
            EmailTemplates.InvitationPlainText(language, acceptUrl));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            EmailTemplates.Wrap(EmailTemplates.InvitationHtmlBody(language, acceptUrl), language), null, "text/html"));

        await RetryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
    }

    public async Task SendWeeklyDigestEmailAsync(
        string toEmail, string bankName, WeeklyDigestStats stats, DateTime weekStartUtc, DateTime weekEndUtc, UiLanguage language,
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
            EmailTemplates.WeeklyDigestSubject(language, bankName, weekStartUtc, weekEndUtc),
            EmailTemplates.WeeklyDigestPlainText(language, bankName, stats, weekStartUtc, weekEndUtc));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            EmailTemplates.Wrap(EmailTemplates.WeeklyDigestHtmlBody(language, bankName, stats, weekStartUtc, weekEndUtc), language), null, "text/html"));

        await RetryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
    }

    public async Task SendAutomationAlertEmailAsync(
        string toEmail, IReadOnlyList<AutomationAlertJob> jobs, UiLanguage language, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };
        using var mail = new MailMessage(
            _options.FromAddress,
            toEmail,
            EmailTemplates.AutomationAlertSubject(language),
            EmailTemplates.AutomationAlertPlainText(language, jobs));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            EmailTemplates.Wrap(EmailTemplates.AutomationAlertHtmlBody(language, jobs), language), null, "text/html"));

        await RetryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
    }
}
