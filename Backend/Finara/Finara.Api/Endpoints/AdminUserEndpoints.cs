using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this WebApplication app)
    {
        var admins = app.MapGroup("/api/admin-users").WithTags("AdminUsers")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        admins.MapGet("/", async (IAdminUserService service, CancellationToken ct) =>
            Results.Ok(await service.GetAdminsAsync(ct)))
            .Produces<IReadOnlyList<AdminUserSummary>>();

        admins.MapPost("/", async (CreateAdminRequest request, IAdminUserService service, CancellationToken ct) =>
        {
            var summary = await service.CreateAdminAsync(request, ct);
            return Results.Created($"/api/admin-users/{summary.Id}", summary);
        })
        .Produces<AdminUserSummary>(StatusCodes.Status201Created);

        admins.MapPost("/{userId:guid}/deactivate", async (Guid userId, ClaimsPrincipal principal, IAdminUserService service, CancellationToken ct) =>
        {
            // JwtBearer'ın varsayılan inbound claim eşlemesi "sub"u ClaimTypes.NameIdentifier'a
            // dönüştürüyor (bkz. RateLimitPolicies.GetPartitionKey'deki aynı ikili kontrol) —
            // sadece JwtRegisteredClaimNames.Sub aranırsa claim hiç bulunamaz.
            var currentUserIdClaim = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserId = Guid.Parse(currentUserIdClaim!);
            var summary = await service.DeactivateAdminAsync(userId, currentUserId, ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Kullanıcı bulunamadı",
                    detail: $"'{userId}' kimlikli kullanıcı bulunamadı.")
                : Results.Ok(summary);
        })
        .Produces<AdminUserSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
