namespace Finara.Domain.Services;

// RFM'in Monetary ekseni ve baskın kategori seçimi, farklı para birimlerindeki tutarları
// TOPLUYORDU: bir müşterinin 1000 TRY + 1000 USD işlemi 2000 olarak hesaplanıyordu, yani
// 1 USD = 1 TRY sayılıyordu (denetimde bulundu — veritabanında 313 USD işlem mevcut). Bu,
// en değerli segmenti (yurtdışı transfer yapan, yatırım/SWIFT kampanyalarının hedefi olan
// müşteriler) sistematik olarak olduğundan düşük skorluyordu.
//
// ÖNEMLİ SINIR: bu sınıf SABİT, yapılandırmadan okunan kurlar kullanır — canlı bir kur
// servisi YOK. Amaç kuruşu kuruşuna doğruluk değil, aynı popülasyon içindeki müşterileri
// KARŞILAŞTIRILABİLİR kılmak (RFM zaten göreli bir sıralamadır). Gerçek bir kur entegrasyonu
// eklendiğinde yalnızca bu sınıfa beslenen kurlar değişir, çağıran kodlar aynı kalır.
public class CurrencyConverter
{
    private readonly IReadOnlyDictionary<string, decimal> ratesToBase;

    public string BaseCurrency { get; }

    public CurrencyConverter(string baseCurrency, IReadOnlyDictionary<string, decimal> ratesToBase)
    {
        if (string.IsNullOrWhiteSpace(baseCurrency))
            throw new ArgumentException("Temel para birimi boş olamaz.", nameof(baseCurrency));

        BaseCurrency = baseCurrency.Trim().ToUpperInvariant();
        this.ratesToBase = ratesToBase;
    }

    // Bilinmeyen bir para birimi 1:1 kabul edilir — eski (hatalı) davranışın ta kendisi, ama
    // bilinçli bir seçim: tanınmayan bir kur yüzünden o müşterinin harcamasını TAMAMEN yok
    // saymak, yaklaşık bir değerle dahil etmekten daha kötü bir sonuç üretirdi. Yeni bir para
    // birimi geldiğinde yapılandırmaya eklenmesi gerekir.
    public decimal ToBase(decimal amount, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return amount;

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized == BaseCurrency)
            return amount;

        return ratesToBase.TryGetValue(normalized, out var rate) ? amount * rate : amount;
    }
}
