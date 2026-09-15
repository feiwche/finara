namespace Finara.Infrastructure.Automation;

public class AutomationWatchdogOptions
{
    public bool Enabled { get; set; } = true;

    // Gözetleyicinin kendisi ucuz bir kontrol (sadece bellekteki son-başarı zamanlarına
    // bakıyor), bu yüzden otomasyon döngüsünden daha sık çalışabilir. Gecikme eşiğini
    // AutomationHealthCheck belirliyor (ilgili işin kendi aralığının 3 katı) — buradaki
    // değer sadece "ne sıklıkla bakılacağı".
    public int CheckIntervalMinutes { get; set; } = 15;
}
