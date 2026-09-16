namespace Finara.Infrastructure.Automation;

// Append-only log tabloları (AuditLogs, NotificationLogs, AiInteractionLogs) hiç
// temizlenmiyordu (denetimde bulundu): bankacılıkta bu tablolar sürekli yazılır ve
// sınırsız büyür — zamanla sorgular yavaşlar, disk dolar.
//
// Saklama süreleri BİLEREK farklı: denetim kaydı yasal/kanıt değeri taşıdığı için en uzun,
// AI etkileşim geçmişi ise tamamen operasyonel olduğu için en kısa süre tutuluyor.
public class LogRetentionOptions
{
    public bool Enabled { get; set; } = true;

    // Temizlik günde bir kez yeterli — sürekli çalışması gereken bir iş değil.
    public int CheckIntervalHours { get; set; } = 24;

    // Denetim kaydı: en uzun saklama. Sıfır ya da negatif verilirse o tablo hiç temizlenmez
    // (mevzuat gereği süresiz saklanması istenirse bu şekilde kapatılabilir).
    public int AuditLogRetentionDays { get; set; } = 730;

    public int NotificationLogRetentionDays { get; set; } = 365;

    public int AiInteractionLogRetentionDays { get; set; } = 90;

    // Tek seferde silinecek satır sayısı — tek dev bir DELETE, tabloyu uzun süre kilitleyip
    // canlı trafiği etkileyebilirdi. Parça parça silinip aralarında nefes alınıyor.
    public int DeleteBatchSize { get; set; } = 1_000;
}
