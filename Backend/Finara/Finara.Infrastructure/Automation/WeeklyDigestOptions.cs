namespace Finara.Infrastructure.Automation;

public class WeeklyDigestOptions
{
    public bool Enabled { get; set; } = true;

    // FinaraAutomationService'teki gerekçenin aynısı — demo/geliştirmede kısa (dakika
    // bazlı), üretimde birkaç saate çekilebilir. Bu, "haftada bir gönder" aralığı DEĞİL,
    // sadece "kimin haftalık özeti gecikmiş, kontrol zamanı geldi mi" kontrolünün ne sıklıkla
    // yapılacağı.
    public int CheckIntervalMinutes { get; set; } = 360;

    // Asıl "haftalık" kuralı — varsayılan 7*24*60=10080 dakika. Demo/geliştirmede kısa bir
    // değere (ör. 5 dk) çekilip gerçek bir e-postanın gönderildiği hızlıca doğrulanabilir.
    public int DigestIntervalMinutes { get; set; } = 10_080;
}
