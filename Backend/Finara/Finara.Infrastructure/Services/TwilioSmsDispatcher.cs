using Finara.Domain.Common;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Finara.Infrastructure.Services;

// MockSmsDispatcher'ın gerçek gönderim yapan karşılığı — IConsentGate kontrolü aynı
// "zorunlu geçiş noktası" deseniyle DispatchAsync'te İLK iş olarak yapılıyor (bkz.
// INotificationDispatcher XML yorumu). TwilioClient.Init bir kez, Program.cs'te
// (kimlik bilgisi varsa) çağrılıyor — bu sınıf sadece MessageResource.CreateAsync çağırıyor.
public class TwilioSmsDispatcher(FinaraDbContext dbContext, IConsentGate consentGate, IOptions<TwilioOptions> options) : INotificationDispatcher
{
    private readonly TwilioOptions _options = options.Value;

    private static readonly IAsyncPolicy RetryPolicy = Policy
        .Handle<Exception>(ex => ex.GetType().Namespace?.StartsWith("Twilio") == true)
        .WaitAndRetryAsync(TransientRetry.Delays);

    public ConsentChannel Channel => ConsentChannel.Sms;

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
        var phoneNumber = await dbContext.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => c.PhoneNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return new NotificationDispatchResult(false, NotificationSkipReasons.CustomerHasNoPhone);

        try
        {
            // SmtpEmailDispatcher'daki aynı desen — cancellationToken önceden ExecuteAsync'e hiç
            // verilmiyordu (denetimde bulundu), bu yüzden istek iptal edilse bile Polly deneme
            // arası bekleme sürelerinde iptali fark etmiyor, gereksiz yeniden denemelere devam
            // ediyordu. Twilio SDK'nın kendisi CreateAsync'e bir token almasa da (delegate içinde
            // kullanılmıyor), Polly'nin bu overload'ı token'ı denemeler ARASINDA kontrol ediyor.
            await RetryPolicy.ExecuteAsync(_ => MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(_options.FromNumber),
                to: new PhoneNumber(phoneNumber)), cancellationToken);

            return new NotificationDispatchResult(true, null);
        }
        catch (Exception ex) when (ex.GetType().Namespace?.StartsWith("Twilio") == true)
        {
            // Aynı gerekçe SmtpEmailDispatcher'da da var: teknik gönderim hatası domain
            // seviyesinde "atlandı" olarak modelleniyor, rıza eksikliğiyle aynı alan.
            return new NotificationDispatchResult(false, $"Twilio hatası: {ex.Message}");
        }
    }
}
