using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;

namespace Finara.Api.Endpoints;

public static class AuditLogEndpoints
{
    public static void MapAuditLogEndpoints(this WebApplication app)
    {
        var auditLogs = app.MapGroup("/api/audit-logs").WithTags("AuditLogs")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        auditLogs.MapGet("/", async (
            Guid? customerId,
            AuditActionType? actionType,
            DateTime? fromUtc,
            DateTime? toUtc,
            IAuditLogRepository repository,
            CancellationToken ct) =>
        {
            var filter = new AuditLogFilter(customerId, actionType, fromUtc, toUtc);
            return Results.Ok(await repository.GetAllAsync(filter, ct));
        })
        .Produces<IReadOnlyList<AuditLogSummary>>();

        // Dashboard hâlâ yukarıdaki "/" ucunu (tam liste) kullanıyor — bu, BackOffice
        // Denetim Kaydı ekranının kendi tablosu için ayrı, sunucu taraflı sayfalanan uç.
        auditLogs.MapGet("/paged", async (
            Guid? customerId,
            AuditActionType? actionType,
            DateTime? fromUtc,
            DateTime? toUtc,
            string? search,
            int? page,
            int? pageSize,
            IAuditLogRepository repository,
            CancellationToken ct) =>
        {
            var filter = new AuditLogFilter(customerId, actionType, fromUtc, toUtc);
            return Results.Ok(await repository.GetPagedAsync(filter, search, page ?? 1, pageSize ?? 20, ct));
        })
        .Produces<PagedResult<AuditLogSummary>>();
    }
}
