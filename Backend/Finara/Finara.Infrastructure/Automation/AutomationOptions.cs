using Finara.Domain.Enums;

namespace Finara.Infrastructure.Automation;

public class AutomationOptions
{
    public bool Enabled { get; set; } = true;

    // Demo/geliştirme ortamında sonucun makul sürede görülebilmesi için varsayılan
    // kısa (dakika bazlı); üretimde appsettings üzerinden gece belirli bir aralığa
    // (ör. her 6 saatte bir) çekilebilir.
    public int IntervalMinutes { get; set; } = 30;

    public int MaxOffersPerCustomer { get; set; } = 5;

    // Denetimde bulundu: otomasyon teklif ÜRETİYOR ama hiç DAĞITMIYORDU — 339 teklife
    // karşılık sadece 6 bildirim vardı (hepsi elle yapılmış testler), Genel Bakış ekranı
    // haklı olarak "%0 müşteriye ulaşıldı" diyordu. Dağıtım yalnızca teklif başına manuel
    // bir uçtan yapılabiliyordu. Bu bayrak kapatılırsa eski (tamamen manuel) davranışa
    // dönülür — müşteriye giden ileti bankada hassas bir konu olduğu için kapatılabilir
    // tutuldu.
    public bool DistributeOffers { get; set; } = true;

    // Her yeni teklif için denenecek kanallar. Rıza kontrolü (IConsentGate) dağıtıcıların
    // İÇİNDE zaten zorunlu bir geçiş noktası — rızası olmayan müşteriye ileti gitmez,
    // "Atlandı: Rıza verilmemiş" olarak Bildirim Kaydı'na yazılır. InAppBanking'in gerçek
    // bir dağıtıcısı olmadığı için varsayılana dâhil edilmedi.
    public ConsentChannel[] DistributionChannels { get; set; } = [ConsentChannel.Sms, ConsentChannel.Email];

    // Tek bir turda yapılacak dağıtım denemesi üst sınırı — ilk çalıştırmada birikmiş
    // yüzlerce teklifin aynı anda işlenip turu (ve gerçek bir SMS/e-posta sağlayıcısını)
    // boğmasını önler; kalanlar bir sonraki turda devam eder. Tur başına MÜŞTERİ sayısı
    // demektir (müşteri başına en fazla bir teklif gönderiliyor, bkz. FinaraAutomationService).
    public int MaxDistributionsPerCycle { get; set; } = 100;

    // Aynı müşteriye iki ileti arasında geçmesi gereken en az süre. Otomasyon 30 dakikada bir
    // çalıştığı için bu koruma olmadan aynı kişiye gün içinde onlarca ileti gidebilirdi
    // (canlı testte bir müşteriye 1 saniyede 10 ileti gittiği görüldü). 0 verilirse bekleme
    // süresi devre dışı kalır.
    public int MinHoursBetweenCustomerMessages { get; set; } = 24;
}
