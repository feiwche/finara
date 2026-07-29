using Finara.Infrastructure.Automation;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Finara.Api.HealthChecks;

// /health/live ve /health/ready'den BİLEREK ayrı — otomasyonun gecikmiş olması API'nin
// trafik almasını engellememeli (o ikisi orkestrasyon/load-balancer'ın kullandığı klasik
// liveness/readiness probe'ları). Bu, harici bir izleme aracının (ör. ücretsiz bir uptime
// monitor) ayrıca yoklayıp gerçek bir alarm kurabileceği, insan gözünün baktığı bir uç.
public class AutomationHealthCheck(
    IAutomationHealthTracker tracker,
    IOptions<AutomationOptions> automationOptions,
    IOptions<WeeklyDigestOptions> digestOptions) : IHealthCheck
{
    // Uygulama az önce başladıysa ilk tur henüz çalışmamış olabilir — bunu "sağlıksız"
    // saymamak için makul bir başlangıç toleransı.
    private static readonly DateTime ProcessStartedAtUtc = DateTime.UtcNow;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var degraded = new List<string>();

        CheckJob("segmentation", automationOptions.Value.Enabled, automationOptions.Value.IntervalMinutes);
        CheckJob("weeklyDigest", digestOptions.Value.Enabled, digestOptions.Value.CheckIntervalMinutes);

        if (degraded.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Gecikmiş otomasyon(lar): {string.Join(", ", degraded)}.", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Tüm otomasyonlar zamanında çalışıyor.", data));

        void CheckJob(string name, bool enabled, int intervalMinutes)
        {
            if (!enabled)
            {
                data[name] = "disabled";
                return;
            }

            // Bir turun ne kadar gecikirse "sorunlu" sayılacağı — kendi aralığının 3 katı
            // (ör. 30 dakikalık bir döngü 90 dakikadır hiç başarılı tamamlanmadıysa gerçekten
            // bir şey yanlış demektir, tek bir gecikmiş tur için yanlış alarm vermez).
            var staleThreshold = TimeSpan.FromMinutes(intervalMinutes * 3);
            var lastSuccess = tracker.GetLastSuccessUtc(name);

            if (lastSuccess is null)
            {
                var sinceStart = DateTime.UtcNow - ProcessStartedAtUtc;
                data[name] = "never run yet";
                if (sinceStart > staleThreshold)
                    degraded.Add($"{name} (uygulama başladığından beri {sinceStart.TotalMinutes:0} dk geçti, hiç başarılı tur yok)");
                return;
            }

            var sinceLastSuccess = DateTime.UtcNow - lastSuccess.Value;
            data[name] = lastSuccess.Value;
            if (sinceLastSuccess > staleThreshold)
                degraded.Add($"{name} (son başarılı tur {sinceLastSuccess.TotalMinutes:0} dk önce)");
        }
    }
}
