namespace Finara.Infrastructure.Automation;

public class AutomationOptions
{
    public bool Enabled { get; set; } = true;

    // Demo/geliştirme ortamında sonucun makul sürede görülebilmesi için varsayılan
    // kısa (dakika bazlı); üretimde appsettings üzerinden gece belirli bir aralığa
    // (ör. her 6 saatte bir) çekilebilir.
    public int IntervalMinutes { get; set; } = 30;

    public int MaxOffersPerCustomer { get; set; } = 5;
}
