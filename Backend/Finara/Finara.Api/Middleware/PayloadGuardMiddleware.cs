using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Finara.Api.Middleware;

public class PayloadGuardOptions
{
    public long MaxBodySizeBytes { get; set; } = 1_048_576; // 1 MB
}

// Gövdesi olan (POST/PUT/PATCH) her isteği iki şeye karşı süzer:
// 1) Content-Length limiti aşan istekler gövde hiç okunmadan reddedilir (413).
// 2) Gövde içinde standart whitespace dışında kontrol karakteri (null-byte, vb.
//    injection denemelerinde sık görülür) varsa istek reddedilir (400).
public class PayloadGuardMiddleware(RequestDelegate next, IOptions<PayloadGuardOptions> options, ILogger<PayloadGuardMiddleware> logger)
{
    private static readonly char[] DisallowedControlChars = Enumerable.Range(0, 32)
        .Select(i => (char)i)
        .Where(c => c != '\t' && c != '\n' && c != '\r')
        .ToArray();

    public async Task InvokeAsync(HttpContext context)
    {
        // multipart/form-data (dosya yükleme) burada atlanıyor: gövde ikili dosya içeriği taşıyor,
        // tamamını string'e okuyup kontrol karakteri taraması yapmak hem anlamsız hem yanlış pozitif
        // üretir. Boyut/tip doğrulaması bu tür uçlarda endpoint'in kendisinde yapılıyor
        // (bkz. CustomerEndpoints "/import").
        if (HasBody(context.Request) && !context.Request.HasFormContentType)
        {
            if (context.Request.ContentLength is { } contentLength && contentLength > options.Value.MaxBodySizeBytes)
            {
                logger.LogWarning("İstek gövdesi boyut sınırını aşıyor. Path={Path}, Size={Size}",
                    context.Request.Path, contentLength);
                await WriteProblemAsync(context, StatusCodes.Status413PayloadTooLarge, "İstek gövdesi çok büyük",
                    $"İstek gövdesi izin verilen {options.Value.MaxBodySizeBytes} bayt sınırını aşıyor.");
                return;
            }

            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (body.IndexOfAny(DisallowedControlChars) >= 0)
            {
                logger.LogWarning("İstek gövdesinde izin verilmeyen kontrol karakteri tespit edildi. Path={Path}", context.Request.Path);
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Geçersiz istek gövdesi",
                    "İstek gövdesinde izin verilmeyen kontrol karakterleri tespit edildi.");
                return;
            }
        }

        await next(context);
    }

    private static bool HasBody(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method) || HttpMethods.IsPatch(request.Method);

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        if (context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
            problemDetails.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
