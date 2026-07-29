using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Finara.Api.HealthChecks;

// ASP.NET Core'un yerleşik health check middleware'i varsayılan olarak sadece genel durumu
// ("Healthy"/"Unhealthy") düz metin olarak yazıyor — AutomationHealthCheck'in Data
// sözlüğündeki (her işin son başarılı çalışma zamanı) asıl bilgi hiç dışarı çıkmıyordu
// (bulundu: canlı testte /health/automation hep sadece "Healthy" döndürüyordu, HANGİ işin
// ne zaman çalıştığı görünmüyordu — tüm özelliğin amacı buydu). Bu, o detayı JSON olarak
// dışa veren basit bir yanıt yazıcısı.
public static class JsonHealthCheckResponseWriter
{
    public static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
