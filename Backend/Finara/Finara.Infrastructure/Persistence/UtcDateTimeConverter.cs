using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Finara.Infrastructure.Persistence;

// SQL Server'ın datetime2 sütunları saat dilimi/Kind bilgisi taşımıyor — EF Core bir
// datetime2'yi okurken her zaman DateTimeKind.Unspecified döner. System.Text.Json,
// Unspecified bir DateTime'ı JSON'a "Z" son eki OLMADAN yazıyor (ör. "2026-07-17T00:00:00"),
// tarayıcı da bunu UTC değil YEREL saat sanıp yanlış yorumluyor (ör. TR'de UTC+3 farkı
// kadar kayıyor, gece yarısına yakın tarihlerde günü bile değiştirebiliyor). Projenin
// "her zaman UTC" kuralı (bkz. ...AtUtc isimlendirmesi) veritabanından okunan her
// DateTime'a burada uygulanıyor — yazarken davranış değişmiyor, okurken Kind=Utc olarak
// damgalanıyor ki JSON'a hep "Z" ile çıksın.
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter() : base(
        v => v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
