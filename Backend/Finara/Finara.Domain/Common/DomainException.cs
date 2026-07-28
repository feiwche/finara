namespace Finara.Domain.Common;

// Sıradan ArgumentException/InvalidOperationException'dan BİLİNÇLİ olarak ayrı: bu, gerçek
// kullanıcı akışlarında (form gönderiminde, bir aksiyona tıklanınca) düzenli olarak karşılaşılan
// iş kuralı ihlalleri için — GlobalExceptionHandler bunu ProblemDetails.Extensions["errorCode"]
// olarak dışa veriyor, frontend'de describeApiError() bu kodu errors.<code> çeviri anahtarına
// çevirip gösteriyor (bkz. Finara.Application/Common/ErrorCodes.cs). Sadece Message'ı (hep
// Türkçe) çeviri sistemine sokmadan doğrudan gösteren toast'ları düzeltmek için eklendi — proje
// genelindeki "yeni her kullanıcı metni baştan i18n'e entegre olsun" kuralının bir parçası.
// Domain katmanındaki ham alan validasyonları (Ad boş olamaz vb.) BİLİNÇLİ OLARAK bu türe
// çevrilmedi — frontend formları zaten bu alanları zorunlu kıldığı için normal kullanımda hiç
// görünmüyorlar; oraya kod eklemek orantısız bir çaba olurdu.
public class DomainException(string code, string message, int statusCode = 400, IReadOnlyDictionary<string, object?>? parameters = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, object?>? Parameters { get; } = parameters;
}
