using Finara.Domain.Common;
using System.Net;
using System.Net.Mail;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;

namespace Finara.Infrastructure.Services;

// MockEmailDispatcher'ın gerçek gönderim yapan karşılığı — IConsentGate kontrolü aynı
// "zorunlu geçiş noktası" deseniyle DispatchAsync'te İLK iş olarak yapılıyor (bkz.
// INotificationDispatcher XML yorumu). Hangi implementasyonun (Mock/Smtp) kullanılacağı
// Program.cs'te appsettings'teki Notifications:Email:Provider'a göre seçiliyor.
public class SmtpEmailDispatcher(FinaraDbContext dbContext, IConsentGate consentGate, IOptions<SmtpOptions> options) : INotificationDispatcher
{
    private readonly SmtpOptions _options = options.Value;

    public ConsentChannel Channel => ConsentChannel.Email;

    public async Task<NotificationDispatchResult> DispatchAsync(
        Guid bankId, Guid customerId, string message, Guid? campaignOfferId, CancellationToken cancellationToken = default)
    {
        var granted = await consentGate.IsConsentGrantedAsync(customerId, Channel, cancellationToken);
        var result = granted
            ? await SendAsync(customerId, message, cancellationToken)
            : new NotificationDispatchResult(false, NotificationSkipReasons.ConsentNotGranted);

        dbContext.NotificationLogs.Add(NotificationLog.Create(
            bankId, customerId, Channel, message, result.Sent, result.SkippedReason, campaignOfferId));
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<NotificationDispatchResult> SendAsync(Guid customerId, string message, CancellationToken cancellationToken)
    {
        var email = await dbContext.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => c.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(email))
            return new NotificationDispatchResult(false, NotificationSkipReasons.CustomerHasNoEmail);

        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                Credentials = new NetworkCredential(_options.Username, _options.Password),
                EnableSsl = _options.EnableSsl
            };
            using var mail = new MailMessage(_options.FromAddress, email, "Finara — Kampanya Teklifi", message);
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                EmailTemplates.Wrap($"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>"), null, "text/html"));

            // YALNIZCA sunucunun mesajı KABUL ETMEDİĞİ kesin olan durumlarda yeniden deniyoruz.
            // Önceden her SmtpException yeniden deneniyordu (denetimde bulundu): sunucu mesajı
            // kabul ettikten SONRA bağlantı düşerse/zaman aşımına uğrarsa, yeniden deneme aynı
            // e-postayı müşteriye İKİNCİ KEZ gönderirdi. Aşağıdaki durum kodları (421/450/452)
            // mesajın işlenmediğini açıkça bildiriyor; GeneralFailure gibi belirsiz olanlar
            // bilinçli olarak DIŞARIDA — bkz. TwilioSmsDispatcher'daki aynı gerekçe.
            var retryPolicy = Policy
                .Handle<SmtpException>(ex => ex.StatusCode is
                    SmtpStatusCode.ServiceNotAvailable or
                    SmtpStatusCode.MailboxBusy or
                    SmtpStatusCode.InsufficientStorage)
                .WaitAndRetryAsync(TransientRetry.Delays);
            await retryPolicy.ExecuteAsync(ct => client.SendMailAsync(mail, ct), cancellationToken);
            return new NotificationDispatchResult(true, null);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            // Teknik gönderim hatası da domain seviyesinde "atlandı" olarak modelleniyor
            // (NotificationLog.Create, Sent=false için SkippedReason zorunlu kılıyor zaten) —
            // rıza eksikliğiyle aynı alan, farklı bir neden metniyle.
            return new NotificationDispatchResult(false, $"SMTP hatası: {ex.Message}");
        }
    }
}
