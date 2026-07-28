using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// User'a EF Core'un ToJson() (EF 8) desteğiyle tek bir JSON kolonu olarak gömülü — yeni bir
// tercih eklemek migration gerektirmiyor. Diğer entity'lerin aksine (private setter + factory)
// burada düz public setter'lar kullanıldı: System.Text.Json'ın JSON kolonunu materyalize etmesi
// için gerekli, ve bu bir iş kuralı taşıyan entity değil, sadece bir tercih torbası — validasyon
// UserPreferencesService'te yapılıyor.
public class UserPreferences
{
    public UiLanguage Language { get; set; } = UiLanguage.Turkish;
    public bool ReceiveWeeklyDigestEmail { get; set; } = true;
    public DateFormatPreference DateFormat { get; set; } = DateFormatPreference.DayMonthYear;
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
    public TableDensity TableDensity { get; set; } = TableDensity.Comfortable;
    public int DefaultPageSize { get; set; } = 20;
}
