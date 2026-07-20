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

    public Guid ResolveBankIdForWrite() => BankId ?? DefaultBank.Id;
}
