namespace Finara.Application.Contracts;

// Genel amaçlı sayfalama zarfı — büyümesi beklenen listelerde (Müşteriler, Denetim Kaydı)
// kullanılıyor. Diğer ekranlar (Kampanyalar, Bankalar vb.) veri hacmi düşük kaldığı için
// bilinçli olarak eski (tam liste) davranışında bırakıldı — ileride gerekirse aynı desen
// tekrar kullanılabilir.
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
