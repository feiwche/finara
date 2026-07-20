using Dapper;
using Finara.Application.Contracts;
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

    public async Task<IReadOnlyList<AuditLogSummary>> GetAllAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT a.Id, a.CustomerId, CONCAT(c.FirstName, N' ', c.LastName) AS CustomerName,
                   a.ActionType, a.Details, a.PerformedByUserId, a.OccurredAtUtc
            FROM AuditLogs a
            LEFT JOIN Customers c ON c.Id = a.CustomerId
            WHERE (@CustomerId IS NULL OR a.CustomerId = @CustomerId)
              AND (@ActionType IS NULL OR a.ActionType = @ActionType)
              AND (@FromUtc IS NULL OR a.OccurredAtUtc >= @FromUtc)
              AND (@ToUtc IS NULL OR a.OccurredAtUtc <= @ToUtc)
            ORDER BY a.OccurredAtUtc DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<AuditLogSummary>(
            new CommandDefinition(sql, new
            {
                filter.CustomerId,
                ActionType = (int?)filter.ActionType,
                filter.FromUtc,
                filter.ToUtc
            }, cancellationToken: cancellationToken));

        // SQL Server datetime2 Kind bilgisi taşımıyor, Dapper okurken Unspecified döner —
        // JSON'a "Z" son eki olmadan yazılırsa tarayıcı UTC yerine yerel saat sanır (bilinen
        // kök neden, bkz. FinaraDbContext.ConfigureConventions — bu Dapper yolunda o global
        // düzeltmenin kapsamına girmiyor, burada elle uygulanmalı).
        return rows.Select(r => r with { OccurredAtUtc = DateTime.SpecifyKind(r.OccurredAtUtc, DateTimeKind.Utc) }).ToList();
    }
}
