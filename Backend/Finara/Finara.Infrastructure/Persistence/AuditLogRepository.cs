using Dapper;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Finara.Infrastructure.Persistence;

// EF Core'un aksine burada sorgular ham SQL, mapping otomatik değil elle yazılan
// sütun listeleriyle yapılıyor — ORM ile manuel erişim arasındaki farkı görmek için.
public class AuditLogRepository : IAuditLogRepository
{
    private readonly string _connectionString;

    public AuditLogRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("'DefaultConnection' bağlantı dizesi bulunamadı.");
    }

    public async Task InsertAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO AuditLogs (Id, CustomerId, ActionType, Details, PerformedByUserId, OccurredAtUtc)
            VALUES (@Id, @CustomerId, @ActionType, @Details, @PerformedByUserId, @OccurredAtUtc);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(sql, auditLog, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditLog>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CustomerId, ActionType, Details, PerformedByUserId, OccurredAtUtc
            FROM AuditLogs
            WHERE CustomerId = @CustomerId
            ORDER BY OccurredAtUtc DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<AuditLog>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
