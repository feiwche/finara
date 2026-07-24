using Finara.Application.Interfaces;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
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

        var segmentationService = new SegmentationService(dbContext, auditLogRepository, systemUser);
        var segmentationResult = await segmentationService.RecalculateAllAsync(stoppingToken);
        logger.LogInformation(
            "Otomatik segmentasyon tamamlandı: {Processed} müşteri işlendi, {Skipped} müşteri atlandı.",
            segmentationResult.CustomersProcessed, segmentationResult.CustomersSkipped);

        var offerService = new NextBestOfferService(dbContext, auditLogRepository, systemUser);
        var offerResult = await offerService.GenerateOffersForAllEligibleCustomersAsync(config.MaxOffersPerCustomer, stoppingToken);
        logger.LogInformation("Otomatik teklif üretimi tamamlandı: {Created} yeni teklif oluşturuldu.", offerResult.OffersCreated);
    }
}
