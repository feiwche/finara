using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Finara.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string Global = "global";
    public const string MockGeneration = "mock-generation";
    public const string TwoFactorVerify = "two-factor-verify";
    public const string DemoLogin = "demo-login";

    // Kimliği doğrulanmış istekler kullanıcı id'sine, diğerleri IP adresine göre
    // bölümleniyor. AddFixedWindowLimiter(policyName, ...) kısayolu BİLEREK kullanılmıyor —
    // partition key olarak policyName sabitini kullanır, yani tüm istemciler tek bir ortak
    // sayacı paylaşır (bir istemcinin limiti doldurması herkesi kilitler).
    public static string GetPartitionKey(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
                return $"user:{userId}";
        }

        return $"ip:{httpContext.Connection.RemoteIpAddress}";
    }
}
