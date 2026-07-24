using Dapper;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Finara.Infrastructure.Persistence;

// AuditLogRepository ile aynı desen (Dapper, ham SQL) — AuditLogs zaten Dapper'la
// yönetiliyor, EF navigasyonu yok. Gün bazlı GROUP BY sonuçları etkinlik olmayan
// günleri hiç döndürmez; grafiğin x-ekseni kopuk görünmesin diye C# tarafında
// boş günler 0 ile dolduruluyor (recursive CTE yerine — veri seti küçük, en fazla
// birkaç ay).
public class ReportService : IReportService
{
    private readonly string _connectionString;
    private readonly ICurrentUserContext _currentUser;

    public ReportService(IConfiguration configuration, ICurrentUserContext currentUser)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("'DefaultConnection' bağlantı dizesi bulunamadı.");
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DailyOfferTrendPoint>> GetOfferTrendAsync(int days, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CAST(a.OccurredAtUtc AS DATE) AS Date,
                   SUM(CASE WHEN a.ActionType = 2 THEN 1 ELSE 0 END) AS Accepted,
                   SUM(CASE WHEN a.ActionType = 3 THEN 1 ELSE 0 END) AS Declined
            FROM AuditLogs a
            WHERE (@BankId IS NULL OR a.BankId = @BankId)
              AND a.OccurredAtUtc >= @FromUtc
              AND a.ActionType IN (2, 3)
            GROUP BY CAST(a.OccurredAtUtc AS DATE)
            ORDER BY Date;
            """;

        var fromUtc = DateTime.UtcNow.Date.AddDays(-(days - 1));

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DailyOfferTrendPoint>(
            new CommandDefinition(sql, new
            {
                BankId = _currentUser.IsSuperAdmin ? (Guid?)null : _currentUser.BankId,
                FromUtc = fromUtc
            }, cancellationToken: cancellationToken));

        var byDate = rows.ToDictionary(r => DateOnly.FromDateTime(r.Date));
        return BuildDateRange(fromUtc, days)
            .Select(date => byDate.TryGetValue(date, out var point)
                ? point with { Date = date.ToDateTime(TimeOnly.MinValue) }
                : new DailyOfferTrendPoint(date.ToDateTime(TimeOnly.MinValue), 0, 0))
            .ToList();
    }

    public async Task<IReadOnlyList<DailyCustomerGrowthPoint>> GetCustomerGrowthAsync(int days, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CAST(a.OccurredAtUtc AS DATE) AS Date, COUNT(*) AS NewCustomers
            FROM AuditLogs a
            WHERE (@BankId IS NULL OR a.BankId = @BankId)
              AND a.OccurredAtUtc >= @FromUtc
              AND a.ActionType = 4
            GROUP BY CAST(a.OccurredAtUtc AS DATE)
            ORDER BY Date;
            """;

        var fromUtc = DateTime.UtcNow.Date.AddDays(-(days - 1));

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DailyCustomerGrowthPoint>(
            new CommandDefinition(sql, new
            {
                BankId = _currentUser.IsSuperAdmin ? (Guid?)null : _currentUser.BankId,
                FromUtc = fromUtc
            }, cancellationToken: cancellationToken));

        var byDate = rows.ToDictionary(r => DateOnly.FromDateTime(r.Date));
        return BuildDateRange(fromUtc, days)
            .Select(date => byDate.TryGetValue(date, out var point)
                ? point with { Date = date.ToDateTime(TimeOnly.MinValue) }
                : new DailyCustomerGrowthPoint(date.ToDateTime(TimeOnly.MinValue), 0))
            .ToList();
    }

    public async Task<OfferImpactSummary> GetOfferImpactSummaryAsync(CancellationToken cancellationToken = default)
    {
        // OfferResponse.Accepted = 1 (bkz. CampaignOffer.cs enum sırası). AVG(ConfidenceScore)
        // hiç teklif yokken NULL döner — COALESCE ile 0'a düşürülüyor.
        const string sql = """
            SELECT
                COUNT(*) AS TotalOffersGenerated,
                SUM(CASE WHEN Response = 1 THEN 1 ELSE 0 END) AS AcceptedOffers,
                COALESCE(AVG(ConfidenceScore), 0) AS AverageConfidenceScore,
                COUNT(DISTINCT CASE WHEN Response = 1 THEN CustomerId END) AS EngagedCustomers
            FROM CampaignOffers
            WHERE (@BankId IS NULL OR BankId = @BankId);
            """;

        await using var connection = new SqlConnection(_connectionString);
        var summary = await connection.QuerySingleAsync<OfferImpactSummary>(
            new CommandDefinition(sql, new
            {
                BankId = _currentUser.IsSuperAdmin ? (Guid?)null : _currentUser.BankId
            }, cancellationToken: cancellationToken));

        return summary;
    }

    public async Task<WeeklyDigestStats> GetWeeklyDigestStatsAsync(Guid bankId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        // CampaignStatus.Active = 1, AuditActionType.CampaignOfferAccepted = 2,
        // CampaignOfferDeclined = 3, CustomerCreated = 4 (bkz. ilgili enum'lar).
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM Customers WHERE BankId = @BankId AND IsErased = 0) AS TotalCustomers,
                (SELECT COUNT(*) FROM Campaigns WHERE BankId = @BankId AND Status = 1) AS ActiveCampaigns,
                (SELECT COUNT(*) FROM AuditLogs WHERE BankId = @BankId AND ActionType = 4 AND OccurredAtUtc >= @SinceUtc) AS NewCustomers,
                (SELECT COUNT(*) FROM AuditLogs WHERE BankId = @BankId AND ActionType = 2 AND OccurredAtUtc >= @SinceUtc) AS OffersAccepted,
                (SELECT COUNT(*) FROM AuditLogs WHERE BankId = @BankId AND ActionType = 3 AND OccurredAtUtc >= @SinceUtc) AS OffersDeclined;
            """;

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<WeeklyDigestStats>(
            new CommandDefinition(sql, new { BankId = bankId, SinceUtc = sinceUtc }, cancellationToken: cancellationToken));
    }

    private static IEnumerable<DateOnly> BuildDateRange(DateTime fromUtc, int days)
    {
        var start = DateOnly.FromDateTime(fromUtc);
        for (var i = 0; i < days; i++)
            yield return start.AddDays(i);
    }
}
