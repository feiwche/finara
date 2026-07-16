using Finara.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Finara.Api.ExceptionHandling;

// Domain'in ArgumentException/InvalidOperationException'ı (Customer.Create, Campaign
// durum makinesi vb. iş kuralı ihlallerinde fırlatılıyor) istemci hatası (400) sayılır;
// geri kalan her şey sunucu hatası (500) olarak, iç detay sızdırmadan raporlanır.
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var id)
            ? id?.ToString()
            : null;

        var (statusCode, title, exposeMessage) = exception switch
        {
            ArgumentException or InvalidOperationException => (StatusCodes.Status400BadRequest, "Geçersiz istek", true),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu", false)
        };

        logger.LogError(exception, "İşlenmeyen hata yakalandı. CorrelationId={CorrelationId}, Path={Path}",
            correlationId, httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exposeMessage ? exception.Message : "Sunucu tarafında beklenmeyen bir hata oluştu.",
            Instance = httpContext.Request.Path
        };
        if (correlationId is not null)
            problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
