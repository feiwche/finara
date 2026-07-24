using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Finara.Application.Interfaces;
using Finara.Application.Security;
using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Api.Security;

public class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsSuperAdmin => User?.FindFirst(ClaimTypes.Role)?.Value == nameof(UserRole.SuperAdmin);

    public Guid? BankId
    {
        get
        {
            var claim = User?.FindFirst(JwtClaimNames.BankId)?.Value;
            return string.IsNullOrEmpty(claim) ? null : Guid.Parse(claim);
        }
    }

    // Token "sub" olarak üretiliyor (AuthService.IssueTokensAsync) AMA JwtBearer'ın
    // varsayılan inbound claim eşlemesi bunu OKURKEN sessizce ClaimTypes.NameIdentifier'a
    // çeviriyor (MapInboundClaims=true varsayılanı) — sadece JwtRegisteredClaimNames.Sub
    // aranırsa claim hiç bulunamaz (canlı testte doğrulandı: PerformedByUserId hep null
    // geliyordu). Aynı iki-aşamalı arama deseni zaten AdminUserEndpoints.cs ve
    // RateLimitPolicies.cs'te kullanılıyordu, burada da tekrarlandı.
    public Guid? UserId
    {
        get
        {
            var claim = User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(claim) ? null : Guid.Parse(claim);
        }
    }

    public Guid ResolveBankIdForWrite() => BankId ?? DefaultBank.Id;
}
