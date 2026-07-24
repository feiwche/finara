using Finara.Application.Interfaces;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finara.Infrastructure.Automation;

// Her aktif BankAdmin'e, kendi bankası için son 7 günün özetini (yeni müşteri, teklif
// kabul/red, aktif kampanya) e-postayla gönderir. SuperAdmin'ler kapsam dışı — tek bir
// bankaya bağlı olmadıkları için "hangi banka için" sorusu net değil, zaten BackOffice'te
// tüm bankaları canlı görebiliyorlar. "Haftada bir" takvim kuralı YOK — her kontrol
// turunda LastWeeklyDigestSentAtUtc'si null olan ya da üzerinden en az 7 gün geçen
// kullanıcılar seçiliyor (bkz. WeeklyDigestOptions XML yorumu).
public class WeeklyDigestService(
    IServiceScopeFactory scopeFactory,
    IOptions<WeeklyDigestOptions> options,
    ILogger<WeeklyDigestService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            logger.LogInformation("Haftalık özet devre dışı (WeeklyDigest:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, config.CheckIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunCycleAsync(config, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Haftalık özet turu başarısız oldu.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(WeeklyDigestOptions config, CancellationToken stoppingToken)
    {
        var digestInterval = TimeSpan.FromMinutes(Math.Max(1, config.DigestIntervalMinutes));
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinaraDbContext>();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IAdminEmailSender>();

        var now = DateTime.UtcNow;
        var cutoff = now - digestInterval;
        var dueUsers = await dbContext.Users
            .Where(u => u.IsActive && u.Role == UserRole.BankAdmin && u.BankId != null
                && (u.LastWeeklyDigestSentAtUtc == null || u.LastWeeklyDigestSentAtUtc <= cutoff))
            .ToListAsync(stoppingToken);

        if (dueUsers.Count == 0)
            return;

        var bankIds = dueUsers.Select(u => u.BankId!.Value).Distinct().ToList();
        var bankNames = await dbContext.Banks
            .Where(b => bankIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, stoppingToken);

        var sentCount = 0;
        foreach (var user in dueUsers)
        {
            var bankId = user.BankId!.Value;
            if (!bankNames.TryGetValue(bankId, out var bankName))
                continue;

            var weekStart = now - digestInterval;
            var stats = await reportService.GetWeeklyDigestStatsAsync(bankId, weekStart, stoppingToken);

            try
            {
                await emailSender.SendWeeklyDigestEmailAsync(user.Email, bankName, stats, weekStart, now, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Bir kullanıcının e-postası başarısız olsa bile diğerlerini engellemesin —
                // ForgotPasswordAsync'teki yutma deseniyle aynı gerekçe (loglanıp geçiliyor).
                logger.LogWarning(ex, "Haftalık özet e-postası gönderilemedi (kullanıcı: {UserId})", user.Id);
                continue;
            }

            user.MarkDigestSent();
            sentCount++;
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        logger.LogInformation("Haftalık özet turu tamamlandı: {Sent} e-posta gönderildi.", sentCount);
    }
}
