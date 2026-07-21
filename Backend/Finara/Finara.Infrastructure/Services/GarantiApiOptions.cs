namespace Finara.Infrastructure.Services;

// appsettings/user-secrets'taki "Garanti" bölümüne bağlanıyor — ClientSecret SmtpOptions.Password
// gibi ASLA appsettings.json'a düz metin yazılmamalı, dotnet user-secrets'ta tutulmalı.
// developers.garantibbva.com.tr'de bir "Uygulama" oluşturulup Account Information +
// Account Transactions API'leri abone edildiğinde ClientId/ClientSecret/TokenUrl/BaseUrl
// portaldan (Key Overview sekmesi) alınıyor.
//
// ConsentId/UnitNum/AccountNum/IBAN: Account Information ve Account Transactions API'leri
// bir "consentId" (rıza/enrollment süreci sonrası alınan kimlik) ZORUNLU kılıyor — gerçek
// üretimde bu, hesap sahibinin bankanın kendi ortamında TPP'ye erişim izni vermesiyle
// oluşuyor (PSD2 tarzı Open Banking akışı). Sandbox dokümantasyonunda örnek/sabit test
// değerleri veriliyor (bkz. varsayılanlar), sandbox'ın bu sabit değerlerle çalıştığı
// varsayılıyor — gerçek kimlik bilgileriyle ilk canlı çağrıda doğrulanmalı.
public class GarantiApiOptions
{
    public string BaseUrl { get; set; } = "https://apis.garantibbva.com.tr:443";
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ConsentId { get; set; } = "1daac6c2-9fd1-55c6-a926-c3f2247405ab";
    public string UnitNum { get; set; } = "295";
    public string AccountNum { get; set; } = "6291296";
    public string Iban { get; set; } = "TR620006200029500006291296";
}
