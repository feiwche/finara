using System.Net;
using System.Net.Mail;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
            : new NotificationDispatchResult(false, "Rıza verilmemiş");

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
            return new NotificationDispatchResult(false, "Müşterinin e-posta adresi yok.");

        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                Credentials = new NetworkCredential(_options.Username, _options.Password),
                EnableSsl = _options.EnableSsl
            };
            using var mail = new MailMessage(_options.FromAddress, email, "Finara — Kampanya Teklifi", message);

            await client.SendMailAsync(mail, cancellationToken);
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
