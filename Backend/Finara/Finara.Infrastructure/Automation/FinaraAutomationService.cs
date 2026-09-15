using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finara.Infrastructure.Automation;

// Manuel "Yeniden Hesapla" / "Teklif Üret" butonları kaldırılmadı — bu servis sadece
// onları periyodik olarak, insan müdahalesi olmadan da tetikler. Segmentasyon RFM
// kurallarına dayanan birincil karar mekanizması olmaya devam ediyor; bu servis
// sadece "ne zaman çalışsın" sorusuna otomatik bir cevap ekliyor.
public class FinaraAutomationService(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationOptions> options,
    IAutomationHealthTracker healthTracker,
    ILogger<FinaraAutomationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            logger.LogInformation("Otomasyon devre dışı (Automation:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, config.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunCycleAsync(config, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Otomasyon turu başarısız oldu.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(AutomationOptions config, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinaraDbContext>();
        var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var systemUser = new SystemCurrentUserContext();
        var chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();

        var segmentationService = new SegmentationService(dbContext, auditLogRepository, systemUser, chatClient);
        var segmentationResult = await segmentationService.RecalculateAllAsync(stoppingToken);
        logger.LogInformation(
            "Otomatik segmentasyon tamamlandı: {Processed} müşteri işlendi, {Skipped} müşteri atlandı.",
            segmentationResult.CustomersProcessed, segmentationResult.CustomersSkipped);

        var offerService = new NextBestOfferService(dbContext, auditLogRepository, systemUser);
        var offerResult = await offerService.GenerateOffersForAllEligibleCustomersAsync(config.MaxOffersPerCustomer, stoppingToken);
        logger.LogInformation("Otomatik teklif üretimi tamamlandı: {Created} yeni teklif oluşturuldu.", offerResult.OffersCreated);

        if (config.DistributeOffers)
            await DistributeNewOffersAsync(config, scope, dbContext, auditLogRepository, systemUser, stoppingToken);

        // Hepsi (segmentasyon + teklif üretimi + dağıtım) istisna fırlatmadan buraya
        // ulaştıysa tur başarılı sayılır — bkz. AutomationHealthTracker'daki gerekçe.
        healthTracker.RecordSuccess("segmentation");
    }

    // Üretilen teklifleri müşteriye ULAŞTIRAN adım. Daha önce bu adım hiç yoktu: teklifler
    // veritabanında birikiyor ama yalnızca biri BackOffice'ten tek tek "Dağıt" derse
    // gidiyordu (denetimde bulundu).
    //
    // "Hiç dağıtılmamış" ölçütü, o teklife ait HİÇ NotificationLog satırı olmaması. Rıza
    // reddi de bir satır yazdığı için (Sent=false, "Atlandı: Rıza verilmemiş") aynı teklif
    // her turda tekrar denenmez — yani rızası olmayan müşteri ileti bombardımanına tutulmaz.
    private async Task DistributeNewOffersAsync(
        AutomationOptions config,
        IServiceScope scope,
        FinaraDbContext dbContext,
        IAuditLogRepository auditLogRepository,
        SystemCurrentUserContext systemUser,
        CancellationToken stoppingToken)
    {
        var channels = config.DistributionChannels;
        if (channels.Length == 0)
            return;

        var undistributedOfferIds = await dbContext.CampaignOffers
            .Where(o => o.Response == OfferResponse.Pending
                && !dbContext.NotificationLogs.Any(n => n.CampaignOfferId == o.Id))
            .OrderBy(o => o.PresentedAtUtc)
            .Take(Math.Max(1, config.MaxDistributionsPerCycle))
            .Select(o => new { o.Id, o.CustomerId })
            .ToListAsync(stoppingToken);

        if (undistributedOfferIds.Count == 0)
            return;

        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();
        var distributionService = new OfferDistributionService(
            dbContext, auditLogRepository, systemUser, scope.ServiceProvider, realtimeNotifier);

        var sent = 0;
        var skipped = 0;
        foreach (var offer in undistributedOfferIds)
        {
            foreach (var channel in channels)
            {
                try
                {
                    var result = await distributionService.DistributeOfferAsync(
                        offer.CustomerId, offer.Id, new DistributeOfferRequest(channel), stoppingToken);

                    if (result is null) continue;
                    if (result.Sent) sent++; else skipped++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Tek bir teklifin/kanalın hatası tüm turu düşürmesin — WeeklyDigestService'teki
                    // aynı "logla ve devam et" deseni.
                    logger.LogWarning(ex, "Teklif dağıtılamadı (teklif: {OfferId}, kanal: {Channel})", offer.Id, channel);
                    skipped++;
                }
            }
        }

        logger.LogInformation(
            "Otomatik teklif dağıtımı tamamlandı: {Offers} teklif işlendi, {Sent} gönderildi, {Skipped} atlandı.",
            undistributedOfferIds.Count, sent, skipped);
    }
}
