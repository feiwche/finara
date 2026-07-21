namespace Finara.Infrastructure.Services;

// appsettings/user-secrets'taki "Garanti" bölümüne bağlanıyor — ClientSecret SmtpOptions.Password
// gibi ASLA appsettings.json'a düz metin yazılmamalı, dotnet user-secrets'ta tutulmalı.
// developers.garantibbva.com.tr'de bir "Uygulama" oluşturulup Account Information +
// Account Transactions API'leri seçildiğinde ClientId/ClientSecret/TokenUrl/BaseUrl
// portaldan alınıyor. TestAccountNumber, sandbox'ın sabit test hesap numarasıdır
// (gerçek/rastgele bir hesap numarası sorgulanamıyor, sandbox önceden tanımlı veri döndürüyor).
public class GarantiApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TestAccountNumber { get; set; } = string.Empty;
}
