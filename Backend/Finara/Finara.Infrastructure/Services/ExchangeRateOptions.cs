namespace Finara.Infrastructure.Services;

// appsettings "ExchangeRates" bölümü. Kurlar SABİTTİR ve elle güncellenir — canlı bir kur
// servisi entegrasyonu yok (bkz. CurrencyConverter'daki gerekçe). Buradaki değerler kuruşu
// kuruşuna doğruluk için değil, RFM'in göreli sıralamasını anlamlı kılmak için var.
public class ExchangeRateOptions
{
    public string BaseCurrency { get; set; } = "TRY";

    // Anahtar: para birimi kodu, değer: 1 birimin kaç BaseCurrency ettiği.
    public Dictionary<string, decimal> RatesToBase { get; set; } = new()
    {
        ["USD"] = 35m,
        ["EUR"] = 38m
    };
}
