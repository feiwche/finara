using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;
using Finara.Infrastructure.Automation;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Finara.Api.HealthChecks;

// /health/automation ucu vardı ama onu YOKLAYAN hiçbir şey yoktu — otomasyon sessizce
// dursa kimse fark etmiyordu (denetimde bulundu). Bu servis o ucu içeriden düzenli olarak
// çalıştırıp gecikme tespit ederse SuperAdmin'lere alarm e-postası gönderir.
//
// Harici bir izleme aracının (uptime monitor) yerine GEÇMEZ, onu tamamlar: harici araç
// "API tamamen çöktü mü" sorusunu yanıtlar (bu servis de çökmüş olurdu), buradaki ise
// "API ayakta ama arka plan işleri durmuş" durumunu yakalar — ki asıl sessiz kalan hata
// tipi budur.
//
// AutomationHealthCheck ile AYNI eşik/karar mantığını kullanıyor (HealthCheckService
// üzerinden gerçek health check'i çalıştırıyor) — gecikme kuralı tek yerde kalsın diye
// kopyalanmadı.
public class AutomationWatchdogService(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationWatchdogOptions> options,
    ILogger<AutomationWatchdogService> logger) : BackgroundService
{
    // Alarm her turda tekrar tekrar gönderilmesin diye durum geçişi izleniyor: sadece
    // sağlıklı→sağlıksız geçişinde e-posta gidiyor. Süreç yeniden başlarsa sıfırlanır ve
    // sorun hâlâ duruyorsa yeniden alarm verir — istenen davranış bu.
    private bool alertSent;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            logger.LogInformation("Otomasyon gözetleyicisi devre dışı (AutomationWatchdog:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, config.CheckIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Otomasyon gözetleme turu başarısız oldu.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains("automation"), stoppingToken);

        if (report.Status == HealthStatus.Healthy)
        {
            if (alertSent)
            {
                logger.LogInformation("Otomasyonlar yeniden normale döndü.");
                alertSent = false;
            }
            return;
        }

        logger.LogError("Otomasyon gecikmesi tespit edildi: {Description}",
            string.Join(" | ", report.Entries.Values.Select(e => e.Description)));

        if (alertSent)
            return;

        var jobs = ExtractDegradedJobs(report);
        if (jobs.Count == 0)
            return;

        // alertSent SADECE gerçekten birine ulaşıldıysa işaretlenir. Önceden SendAlertsAsync
        // void'di ve alıcı listesi okunamadığında (ör. otomasyonu durduran şey veritabanının
        // kendisiyse) hatayı yutup normal dönüyordu — çağıran da "alarm gitti" sanıp bayrağı
        // set ediyor, böylece o olay için bir daha HİÇ alarm üretilmiyordu. Gözetleyicinin
        // var oluş sebebi tam olarak bu tür sessiz başarısızlıklar olduğu için, canlı testte
        // bulunan bu davranış düzeltildi: ulaşılamazsa bayrak set edilmez ve bir sonraki
        // turda yeniden denenir.
        alertSent = await SendAlertsAsync(scope.ServiceProvider, jobs, stoppingToken);
    }

    // AutomationHealthCheck, gecikmiş iş ADLARINI Data[DegradedJobsKey] içinde virgülle
    // ayrılmış olarak veriyor; her işin son başarı zamanı da aynı Data sözlüğünde kendi
    // adıyla duruyor (DateTime ya da "never run yet" metni).
    private static List<AutomationAlertJob> ExtractDegradedJobs(HealthReport report)
    {
        var jobs = new List<AutomationAlertJob>();

        foreach (var entry in report.Entries.Values)
        {
            if (!entry.Data.TryGetValue(AutomationHealthCheck.DegradedJobsKey, out var degradedRaw))
                continue;

            var names = (degradedRaw as string ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var name in names)
            {
                var lastSuccess = entry.Data.TryGetValue(name, out var value) && value is DateTime dt
                    ? dt
                    : (DateTime?)null;
                jobs.Add(new AutomationAlertJob(name, lastSuccess));
            }
        }

        return jobs;
    }

    // true: en az bir alıcıya gerçekten gönderildi. false: hiç kimseye ulaşılamadı
    // (alıcı listesi okunamadı ya da hiç aktif SuperAdmin yok) — çağıran bunu "alarm
    // verildi" saymamalı.
    private async Task<bool> SendAlertsAsync(IServiceProvider services, IReadOnlyList<AutomationAlertJob> jobs, CancellationToken stoppingToken)
    {
        var dbContext = services.GetRequiredService<FinaraDbContext>();
        var emailSender = services.GetRequiredService<IAdminEmailSender>();

        // Alarm SuperAdmin'lere gidiyor — sistemin bütününden (tüm bankaların otomasyonu
        // tek bir arka plan servisinde çalışıyor) sorumlu olan rol bu. BankAdmin'ler
        // haftalık özet alıyor, operasyonel altyapı uyarısı onların işi değil.
        //
        // Otomasyonun durma sebebi veritabanının kendisi olabilir — o durumda alıcı listesi
        // de okunamaz. Bu ayrıca ele alınıyor ki log'da "alarm gönderilemedi, çünkü DB'ye
        // ulaşılamıyor" net görünsün; gecikmeyi bildiren ERROR zaten yukarıda yazıldı.
        List<AlertRecipient> recipients;
        try
        {
            recipients = await dbContext.Users
                .Where(u => u.IsActive && u.Role == UserRole.SuperAdmin)
                .Select(u => new AlertRecipient(u.Id, u.Email, u.Preferences.Language))
                .ToListAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Otomasyon alarmı gönderilemedi: alıcı listesi veritabanından okunamadı.");
            return false;
        }

        if (recipients.Count == 0)
        {
            logger.LogWarning("Otomasyon alarmı gönderilecek aktif SuperAdmin bulunamadı.");
            return false;
        }

        var sent = 0;
        foreach (var recipient in recipients)
        {
            try
            {
                await emailSender.SendAutomationAlertEmailAsync(recipient.Email, jobs, recipient.Language, stoppingToken);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // WeeklyDigestService'teki aynı gerekçe: bir alıcının e-postası başarısız
                // olsa bile diğerlerini engellemesin.
                logger.LogWarning(ex, "Otomasyon alarm e-postası gönderilemedi (kullanıcı: {UserId})", recipient.Id);
            }
        }

        if (sent == 0)
        {
            logger.LogError("Otomasyon alarmı hiçbir alıcıya ulaştırılamadı ({Count} deneme).", recipients.Count);
            return false;
        }

        logger.LogWarning("Otomasyon alarmı {Sent}/{Count} SuperAdmin'e gönderildi.", sent, recipients.Count);
        return true;
    }

    private record AlertRecipient(Guid Id, string Email, UiLanguage Language);
}
