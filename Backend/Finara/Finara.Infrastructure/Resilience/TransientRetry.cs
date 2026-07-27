namespace Finara.Infrastructure.Resilience;

// SMTP (System.Net.Mail) ve Twilio SDK'ları IHttpClientFactory üzerinden çalışmadığı için
// Garanti/Ollama HttpClient'larında kullanılan Microsoft.Extensions.Http.Polly (Program.cs)
// buraya uygulanamıyor — bu yüzden aynı "3 deneme, üstel geri çekilme" zamanlamasını düz
// Polly policy'siyle kullanan dispatcher'lar bu sabit gecikme dizisini paylaşıyor. Hangi
// exception tipinin "tekrar denenebilir" sayılacağı (SmtpException, Twilio hataları vb.)
// her dispatcher'da zaten var olan catch bloklarıyla aynı yerde kalıyor — burada sadece
// zamanlama tekrarlanıyor.
public static class TransientRetry
{
    public static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1)
    ];
}
