using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finara.Infrastructure.Services;

public class OfferDistributionService(
    FinaraDbContext dbContext,
    IAuditLogRepository auditLogRepository,
    ICurrentUserContext currentUser,
    IServiceProvider serviceProvider,
    IRealtimeNotifier realtimeNotifier)
    : IOfferDistributionService
{
    public async Task<NotificationDispatchResult?> DistributeOfferAsync(
        Guid customerId, Guid offerId, DistributeOfferRequest request, CancellationToken cancellationToken = default)
    {
        var offer = await dbContext.CampaignOffers
            .Include(o => o.Campaign)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.CustomerId == customerId, cancellationToken);
        if (offer is null || (!currentUser.IsSuperAdmin && offer.BankId != currentUser.BankId))
            return null;

        var campaignName = offer.Campaign?.Name ?? "Kampanya";

        var message = $"{campaignName} teklifiniz sizi bekliyor! Güven skoru: %{offer.ConfidenceScore:0}";

        // InAppBanking için rıza takip ediliyor ama gerçek bir dispatcher YOK (bankaya
        // veri geri gönderen bir OUTBOUND entegrasyon gerektiriyor, kapsam dışı — bkz.
        // ConsentChannel XML yorumu). GetRequiredKeyedService'in ham DI hatası yerine
        // burada açıkça, TR mesajla ele alınıyor. Diğer kanallarla AYNI şekilde hem
        // NotificationLog'a hem AuditLog'a yazılıyor — Bildirim Kaydı ekranında bu
        // denemenin de görünmesi gerekiyor, sadece AuditLog'da kalması tutarsızlık
        // yaratırdı (canlı testte bulundu: InAppBanking denemesi AuditLog'da vardı ama
        // /api/notifications listesinde hiç görünmüyordu).
        var result = request.Channel == ConsentChannel.InAppBanking
            ? new NotificationDispatchResult(false, "Bu kanal için henüz bir gönderim entegrasyonu yok.")
            : await serviceProvider.GetRequiredKeyedService<INotificationDispatcher>(request.Channel)
                .DispatchAsync(offer.BankId, customerId, message, offer.Id, cancellationToken);

        if (request.Channel == ConsentChannel.InAppBanking)
        {
            dbContext.NotificationLogs.Add(NotificationLog.Create(
                offer.BankId, customerId, request.Channel, message, result.Sent, result.SkippedReason, offer.Id));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var detail = result.Sent ? $"{campaignName} / gönderildi" : $"{campaignName} / atlandı: {result.SkippedReason}";
        await auditLogRepository.InsertAsync(
            AuditLog.Create(offer.BankId, offer.CustomerId, AuditActionType.OfferDistributed, detail, currentUser.UserId),
            cancellationToken);

        // Gerçek zamanlı yayın — dispatcher'ın (Mock/Smtp/Twilio) az önce yazdığı satırı
        // geri okuyup yayınlıyoruz (InAppBanking için de aynı sorgu çalışır, o satırı
        // yukarıda kendimiz eklemiştik) — dört dispatcher'ın da değiştirilmesine gerek
        // kalmadan tek bir noktadan gerçek DB kaydıyla birebir eşleşen bir olay üretiyor.
        var freshLog = await dbContext.NotificationLogs.AsNoTracking()
            .Where(n => n.CustomerId == customerId && n.Channel == request.Channel && n.CampaignOfferId == offer.Id)
            .OrderByDescending(n => n.SentAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (freshLog is not null)
        {
            var customer = await dbContext.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => new { c.FirstName, c.LastName })
                .FirstOrDefaultAsync(cancellationToken);
            var customerName = customer is null ? "Müşteri" : $"{customer.FirstName} {customer.LastName}";

            await realtimeNotifier.NotifyOfferDistributedAsync(
                offer.BankId,
                new NotificationLogSummary(
                    freshLog.Id, customerId, customerName, offer.Id,
                    request.Channel, message, result.Sent, result.SkippedReason, freshLog.SentAtUtc),
                cancellationToken);
        }

        return result;
    }
}
