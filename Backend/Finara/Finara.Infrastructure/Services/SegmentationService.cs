using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Services;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class SegmentationService(FinaraDbContext dbContext) : ISegmentationService
{
    // RFM ve baskın kategori hesaplaması bu pencereyle sınırlı — çok eski işlemler
    // güncel davranışı yansıtmadığı için segmentasyona dahil edilmiyor.
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(90);

    public async Task<RecalculateSegmentsResult> RecalculateAllAsync(CancellationToken cancellationToken = default)
    {
        var referenceUtc = DateTime.UtcNow;
        var lookbackStart = referenceUtc - LookbackWindow;

        var activitySummaries = await dbContext.Transactions
            .Where(t => t.OccurredAtUtc >= lookbackStart)
            .GroupBy(t => t.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                LastTransactionUtc = g.Max(t => t.OccurredAtUtc),
                Count = g.Count(),
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync(cancellationToken);

        var totalCustomers = await dbContext.Customers.CountAsync(cancellationToken);

        if (activitySummaries.Count == 0)
            return new RecalculateSegmentsResult(0, totalCustomers);

        var rfmInputs = activitySummaries
            .Select(a => new RfmCalculator.CustomerActivity(a.CustomerId, a.LastTransactionUtc, a.Count, a.Total))
            .ToList();
        var rfmScores = RfmCalculator.CalculateScores(rfmInputs, referenceUtc).ToDictionary(s => s.CustomerId);

        // Baskın kategori: pencere içinde en çok harcanan kategori (tutar bazlı).
        var categoryTotals = await dbContext.Transactions
            .Where(t => t.OccurredAtUtc >= lookbackStart)
            .GroupBy(t => new { t.CustomerId, t.Category })
            .Select(g => new { g.Key.CustomerId, g.Key.Category, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var dominantCategories = categoryTotals
            .GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Total).First().Category);

        var customerIds = rfmScores.Keys.ToList();
        var existingActiveSegments = await dbContext.Segments
            .Where(s => customerIds.Contains(s.CustomerId) && s.IsActive)
            .ToListAsync(cancellationToken);
        var existingActiveByCustomer = existingActiveSegments.ToDictionary(s => s.CustomerId);

        foreach (var customerId in customerIds)
        {
            var rfm = rfmScores[customerId];
            var propensity = RfmCalculator.DeterminePropensity(rfm.TotalScore);
            var dominantCategory = dominantCategories.GetValueOrDefault(customerId);

            if (existingActiveByCustomer.TryGetValue(customerId, out var existing))
                existing.Deactivate();

            dbContext.Segments.Add(Segment.Create(
                customerId, rfm.RecencyScore, rfm.FrequencyScore, rfm.MonetaryScore, propensity, dominantCategory));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RecalculateSegmentsResult(customerIds.Count, totalCustomers - customerIds.Count);
    }

    public async Task<SegmentSummary?> GetActiveSegmentAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var segment = await dbContext.Segments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive, cancellationToken);

        return segment is null
            ? null
            : new SegmentSummary(
                segment.CustomerId, segment.RecencyScore, segment.FrequencyScore, segment.MonetaryScore,
                segment.GetTotalScore(), segment.Propensity, segment.DominantCategory, segment.CalculatedAtUtc);
    }
}
