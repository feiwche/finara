namespace Finara.Infrastructure.Services;

// appsettings/user-secrets'taki "Smtp" bölümüne bağlanıyor — Username/Password
// SIR HİÇBİR ZAMAN appsettings.json'a düz metin yazılmamalı, dotnet user-secrets'ta tutulmalı
// (bkz. Finara.Domain.Tests dışındaki tüm bağlantı dizesi/parola kararlarıyla aynı disiplin).
public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}
