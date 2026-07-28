using Finara.Api.Middleware;
using Finara.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finara.Api.ExceptionHandling;

// Domain'in ArgumentException/InvalidOperationException'ı (Customer.Create, Campaign
// durum makinesi vb. iş kuralı ihlallerinde fırlatılıyor) istemci hatası (400) sayılır;
// DbUpdateException (benzersiz index/FK ihlali gibi DB kısıtlaması ihlalleri) 409 sayılır —
// bunlar da istemcinin gönderdiği veriden kaynaklanır, sunucu hatası değildir. Geri kalan
// her şey 500 olarak, iç detay (SQL/stack trace) sızdırılmadan raporlanır.
//
// DomainException (yeni) diğerlerinden AYRI ele alınıyor — Message hâlâ (geriye dönük
// uyumluluk ve loglama için) hep Türkçe, ama ayrıca ProblemDetails.Extensions["errorCode"]
// olarak stabil bir kod dışa veriliyor. Frontend describeApiError() bu kodu errors.<code>
// çeviri anahtarına çevirip gösteriyor — Message'ı hiç göstermiyor (bkz. o dosyadaki yorum).
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var id)
            ? id?.ToString()
            : null;

        var (statusCode, title, detail) = exception switch
        {
            DomainException de => (de.StatusCode, de.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => "Kimlik doğrulama başarısız",
                StatusCodes.Status403Forbidden => "Yetkisiz erişim",
                StatusCodes.Status409Conflict => "Çakışan veri",
                _ => "Geçersiz istek"
            }, de.Message),

            ArgumentException or InvalidOperationException =>
                (StatusCodes.Status400BadRequest, "Geçersiz istek", exception.Message),

            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Kimlik doğrulama başarısız", exception.Message),

            // Minimal API'nin istek gövdesini (JSON) okuyamadığı durumlarda fırlattığı hata —
            // bozuk/eksik body, bilinmeyen enum değeri vb. İstemci hatasıdır, sunucu hatası değil.
            Microsoft.AspNetCore.Http.BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "İstek gövdesi okunamadı", exception.Message),

            // Örn. e-posta benzersizlik ihlali, Segment'in tekil-aktif filtered index'i.
            // SQL/tablo adı gibi iç detaylar sızdırılmasın diye genel bir mesaj veriliyor.
            DbUpdateException =>
                (StatusCodes.Status409Conflict, "Çakışan veri",
                 "Bu işlem mevcut bir kayıtla çakışıyor (örn. e-posta adresi zaten kullanımda)."),

            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu",
                  "Sunucu tarafında beklenmeyen bir hata oluştu.")
        };

        logger.LogError(exception, "İşlenmeyen hata yakalandı. CorrelationId={CorrelationId}, Path={Path}",
            correlationId, httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        if (correlationId is not null)
            problemDetails.Extensions["correlationId"] = correlationId;
        if (exception is DomainException domainException)
        {
            problemDetails.Extensions["errorCode"] = domainException.Code;
            if (domainException.Parameters is not null)
                problemDetails.Extensions["errorParams"] = domainException.Parameters;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
