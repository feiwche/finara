using System.Collections.Concurrent;

namespace Finara.Infrastructure.Automation;

// Arka plan otomasyonları (segmentasyon/teklif üretimi, haftalık özet) daha önce sadece
// hata durumunda LOG'a yazıyordu — bir tur sessizce çökerse (ör. bir müşterinin verisindeki
// beklenmedik bir durum yüzünden) kimse fark etmiyordu, sadece log'ları izleyen biri olsaydı
// görürdü. Bu, her başarılı turun ZAMANINI tutan tek bir kayıt noktası — AutomationHealthCheck
// bunu okuyup /health/automation ucundan dışa veriyor, böylece harici bir izleme aracı
// (ör. ücretsiz bir uptime monitor) bu ucu düzenli yoklayıp gerçek bir alarm kurabilir.
public interface IAutomationHealthTracker
{
    void RecordSuccess(string jobName);
    DateTime? GetLastSuccessUtc(string jobName);
}

public class AutomationHealthTracker : IAutomationHealthTracker
{
    private readonly ConcurrentDictionary<string, DateTime> lastSuccessUtc = new();

    public void RecordSuccess(string jobName) => lastSuccessUtc[jobName] = DateTime.UtcNow;

    public DateTime? GetLastSuccessUtc(string jobName) =>
        lastSuccessUtc.TryGetValue(jobName, out var value) ? value : null;
}
