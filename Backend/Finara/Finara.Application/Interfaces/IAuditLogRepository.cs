using Finara.Domain.Entities;

namespace Finara.Application.Interfaces;

public interface IAuditLogRepository
{
    Task InsertAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}
