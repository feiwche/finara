using Finara.Application.Contracts;
using Finara.Domain.Entities;

namespace Finara.Application.Interfaces;

public interface IAuditLogRepository
{
    Task InsertAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    // BackOffice Denetim Kaydı ekranı için — müşteri adı Customers ile join edilerek
    // döndürülüyor, filtreler opsiyonel (hepsi null ise tüm kayıtlar).
    Task<IReadOnlyList<AuditLogSummary>> GetAllAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);
}
