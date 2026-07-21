namespace Finara.Infrastructure.Services;

// appsettings/user-secrets'taki "Twilio" bölümüne bağlanıyor — AuthToken SmtpOptions.Password
// gibi ASLA appsettings.json'a düz metin yazılmamalı, dotnet user-secrets'ta tutulmalı.
public class TwilioOptions
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
}
