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
    private readonly ICurrentUserContext _currentUser;

    public AuditLogRepository(IConfiguration configuration, ICurrentUserContext currentUser)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("'DefaultConnection' bağlantı dizesi bulunamadı.");
        _currentUser = currentUser;
    }

    public async Task InsertAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO AuditLogs (Id, BankId, CustomerId, ActionType, Details, PerformedByUserId, OccurredAtUtc)
            VALUES (@Id, @BankId, @CustomerId, @ActionType, @Details, @PerformedByUserId, @OccurredAtUtc);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(sql, auditLog, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditLogSummary>> GetAllAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        // SuperAdmin tüm bankaları görür (BankId parametresi NULL geçilir, WHERE koşulu
        // devre dışı kalır) — BankAdmin için @BankId sabitlenir.
        // CONCAT, NULL'ları boş dizeye çevirdiği için müşterisiz olaylarda (ör. yönetici
        // hesabı işlemleri, CustomerId NULL) tek bir boşluk karakteri dönerdi — NULLIF ile
        // bu durumda gerçekten NULL dönüyor, frontend'in "?? '—'" düşüşü doğru tetiklensin diye.
        const string sql = """
            SELECT a.Id, a.CustomerId, NULLIF(CONCAT(c.FirstName, N' ', c.LastName), N' ') AS CustomerName,
                   a.ActionType, a.Details, a.PerformedByUserId, a.OccurredAtUtc
            FROM AuditLogs a
            LEFT JOIN Customers c ON c.Id = a.CustomerId
            WHERE (@BankId IS NULL OR a.BankId = @BankId)
              AND (@CustomerId IS NULL OR a.CustomerId = @CustomerId)
              AND (@ActionType IS NULL OR a.ActionType = @ActionType)
              AND (@FromUtc IS NULL OR a.OccurredAtUtc >= @FromUtc)
              AND (@ToUtc IS NULL OR a.OccurredAtUtc <= @ToUtc)
            ORDER BY a.OccurredAtUtc DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<AuditLogSummary>(
            new CommandDefinition(sql, new
            {
                BankId = _currentUser.IsSuperAdmin ? (Guid?)null : _currentUser.BankId,
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

    public async Task<PagedResult<AuditLogSummary>> GetPagedAsync(
        AuditLogFilter filter, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Üst sınır olmadan çok büyük bir "page" değeri (Skip = (page-1)*pageSize) int32
        // taşmasına uğrayıp negatif bir OFFSET üretebilir — SQL Server bunu reddedip
        // yakalanmamış bir SqlException'a (500) düşürürdü.
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // COUNT ve veri sorgusu aynı WHERE koşulunu paylaşıyor — Dapper'da parametrik SQL
        // string interpolation ile birleştirilirken sabit (kullanıcı girdisi içermeyen) bir
        // metin parçası olduğu için SQL injection riski yok, sadece tekrarı önlüyor.
        const string whereClause = """
            WHERE (@BankId IS NULL OR a.BankId = @BankId)
              AND (@CustomerId IS NULL OR a.CustomerId = @CustomerId)
              AND (@ActionType IS NULL OR a.ActionType = @ActionType)
              AND (@FromUtc IS NULL OR a.OccurredAtUtc >= @FromUtc)
              AND (@ToUtc IS NULL OR a.OccurredAtUtc <= @ToUtc)
              AND (@Search IS NULL OR NULLIF(CONCAT(c.FirstName, N' ', c.LastName), N' ') LIKE @Search)
            """;

        var countSql = $"""
            SELECT COUNT(*)
            FROM AuditLogs a
            LEFT JOIN Customers c ON c.Id = a.CustomerId
            {whereClause};
            """;

        var dataSql = $"""
            SELECT a.Id, a.CustomerId, NULLIF(CONCAT(c.FirstName, N' ', c.LastName), N' ') AS CustomerName,
                   a.ActionType, a.Details, a.PerformedByUserId, a.OccurredAtUtc
            FROM AuditLogs a
            LEFT JOIN Customers c ON c.Id = a.CustomerId
            {whereClause}
            ORDER BY a.OccurredAtUtc DESC, a.Id DESC
            OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            BankId = _currentUser.IsSuperAdmin ? (Guid?)null : _currentUser.BankId,
            filter.CustomerId,
            ActionType = (int?)filter.ActionType,
            filter.FromUtc,
            filter.ToUtc,
            Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
            Skip = (page - 1) * pageSize,
            PageSize = pageSize
        };

        await using var connection = new SqlConnection(_connectionString);
        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<AuditLogSummary>(new CommandDefinition(dataSql, parameters, cancellationToken: cancellationToken));
        var items = rows.Select(r => r with { OccurredAtUtc = DateTime.SpecifyKind(r.OccurredAtUtc, DateTimeKind.Utc) }).ToList();

        return new PagedResult<AuditLogSummary>(items, totalCount, page, pageSize);
    }
}
