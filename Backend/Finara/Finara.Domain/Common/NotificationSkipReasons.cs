namespace Finara.Domain.Common;

// NotificationLog.SkippedReason için stabil kodlar — ErrorCodes ile AYNI gerekçe: bu değer
// Bildirim Kaydı ekranında personele gösteriliyor ve önceden SABİT TÜRKÇE metin olarak
// yazılıyordu ("Rıza verilmemiş"), yani İngilizce/Almanca arayüz kullanan personel ham
// Türkçe görüyordu (denetimde bulundu).
//
// Frontend bu kodları `notificationLog.skipReason.*` çeviri anahtarlarına eşler; tanımadığı
// bir değeri OLDUĞU GİBİ gösterir. Bu sayede hem veritabanındaki ESKİ Türkçe kayıtlar hem de
// teknik hata metinleri ("SMTP hatası: ...") ayrı bir uyumluluk katmanı gerektirmeden
// çalışmaya devam eder — teknik hata detayları zaten çevrilebilir şeyler değil.
public static class NotificationSkipReasons
{
    public const string ConsentNotGranted = "CONSENT_NOT_GRANTED";
    public const string CustomerHasNoEmail = "CUSTOMER_HAS_NO_EMAIL";
    public const string CustomerHasNoPhone = "CUSTOMER_HAS_NO_PHONE";
    public const string CampaignNotActive = "CAMPAIGN_NOT_ACTIVE";
    public const string ChannelNotIntegrated = "CHANNEL_NOT_INTEGRATED";
}
