using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finara.Infrastructure.Automation;

// Append-only log tablolarını saklama süresine göre budar (bkz. LogRetentionOptions).
// Bu tablolar hiç temizlenmiyordu; bankacılık ürününde sürekli yazıldıkları için sınırsız
// büyüyüp sorgu performansını ve diski tehdit ediyorlardı (denetimde bulundu).
//
// AuditLogs BİLEREK EF üzerinden değil ham SQL ile siliniyor: bu tablonun DbSet'i yok
// (Dapper ile yönetiliyor, bkz. AuditLogRepository). Diğer ikisi EF'in ExecuteDeleteAsync'i
// ile — tek sorguda siliniyor, satırları belleğe çekmeden.
public class LogRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<LogRetentionOptions> options,
    ILogger<LogRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            logger.LogInformation("Log temizliği devre dışı (LogRetention:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, config.CheckIntervalHours));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunCycleAsync(config, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Log temizleme turu başarısız oldu.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(LogRetentionOptions config, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinaraDbContext>();

        var notificationLogs = await DeleteInBatchesAsync(
            config.NotificationLogRetentionDays, config.DeleteBatchSize, stoppingToken,
            cutoff => dbContext.NotificationLogs.Where(n => n.SentAtUtc < cutoff));

        var aiLogs = await DeleteInBatchesAsync(
            config.AiInteractionLogRetentionDays, config.DeleteBatchSize, stoppingToken,
            cutoff => dbContext.AiInteractionLogs.Where(a => a.CreatedAtUtc < cutoff));

        var auditLogs = await DeleteAuditLogsAsync(dbContext, config, stoppingToken);

        if (notificationLogs + aiLogs + auditLogs > 0)
        {
            logger.LogInformation(
                "Log temizliği tamamlandı: {Notifications} bildirim, {AiLogs} AI etkileşimi, {AuditLogs} denetim kaydı silindi.",
                notificationLogs, aiLogs, auditLogs);
        }
    }

    private static async Task<int> DeleteInBatchesAsync<T>(
        int retentionDays,
        int batchSize,
        CancellationToken stoppingToken,
        Func<DateTime, IQueryable<T>> expiredQuery) where T : class
    {
        if (retentionDays <= 0)
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var effectiveBatch = Math.Max(1, batchSize);
        var total = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var deleted = await expiredQuery(cutoff).Take(effectiveBatch).ExecuteDeleteAsync(stoppingToken);
            total += deleted;
            if (deleted < effectiveBatch)
                break;
        }

        return total;
    }

    private static async Task<int> DeleteAuditLogsAsync(
        FinaraDbContext dbContext, LogRetentionOptions config, CancellationToken stoppingToken)
    {
        if (config.AuditLogRetentionDays <= 0)
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-config.AuditLogRetentionDays);
        var effectiveBatch = Math.Max(1, config.DeleteBatchSize);
        var total = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            // AuditLogs'un DbSet'i yok (Dapper ile yönetiliyor) — TOP(n) ile parça parça siliniyor.
            var deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE TOP ({effectiveBatch}) FROM AuditLogs WHERE OccurredAtUtc < {cutoff}",
                stoppingToken);
            total += deleted;
            if (deleted < effectiveBatch)
                break;
        }

        return total;
    }
}
