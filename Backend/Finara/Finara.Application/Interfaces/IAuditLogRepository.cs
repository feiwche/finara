using Finara.Application.Contracts;
using Finara.Domain.Entities;

namespace Finara.Application.Interfaces;

public interface IAuditLogRepository
{
    Task InsertAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    // BackOffice Denetim Kaydı ekranı için — müşteri adı Customers ile join edilerek
    // döndürülüyor, filtreler opsiyonel (hepsi null ise tüm kayıtlar).
    Task<IReadOnlyList<AuditLogSummary>> GetAllAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);

    // GetAllAsync'in sunucu taraflı sayfalanan karşılığı — Dashboard hâlâ GetAllAsync'i
    // (tam liste, sadece .Count() için) kullandığından o metoda dokunulmadı, bu ayrı bir uç.
    Task<PagedResult<AuditLogSummary>> GetPagedAsync(AuditLogFilter filter, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}
