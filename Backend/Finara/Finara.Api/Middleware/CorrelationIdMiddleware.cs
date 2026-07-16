namespace Finara.Api.Middleware;

// İstemci bir X-Correlation-Id header'ı gönderirse onu kullanır (mikroservisler arası
// zincirleme senaryosunda üst servisin id'sini korumak için), göndermezse yeni üretir.
// Response header'ında geri döner ve tüm loglara BeginScope ile eklenir.
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
