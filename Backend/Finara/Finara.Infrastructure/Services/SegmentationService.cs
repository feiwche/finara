using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Domain.Services;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class SegmentationService(FinaraDbContext dbContext, IAuditLogRepository auditLogRepository, ICurrentUserContext currentUser) : ISegmentationService
{
    // RFM ve baskın kategori hesaplaması bu pencereyle sınırlı — çok eski işlemler
    // güncel davranışı yansıtmadığı için segmentasyona dahil edilmiyor.
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(90);

    public async Task<RecalculateSegmentsResult> RecalculateAllAsync(CancellationToken cancellationToken = default)
    {
        var referenceUtc = DateTime.UtcNow;
        var lookbackStart = referenceUtc - LookbackWindow;

        // SuperAdmin tüm bankalar için yeniden hesaplar, BankAdmin sadece kendi bankası için.
        var transactionsQuery = dbContext.Transactions.AsQueryable();
        var customersQuery = dbContext.Customers.AsQueryable();
        var segmentsQuery = dbContext.Segments.AsQueryable();
        if (!currentUser.IsSuperAdmin)
        {
            transactionsQuery = transactionsQuery.Where(t => t.BankId == currentUser.BankId);
            customersQuery = customersQuery.Where(c => c.BankId == currentUser.BankId);
            segmentsQuery = segmentsQuery.Where(s => s.BankId == currentUser.BankId);
        }

        var activitySummaries = await transactionsQuery
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

        var totalCustomers = await customersQuery.CountAsync(cancellationToken);
        var activeCustomerIds = activitySummaries.Select(a => a.CustomerId).ToHashSet();

        // Aktivite penceresinde artık görünmeyen ama önceden aktif segmenti olan müşteriler —
        // bunların segmenti deaktive edilmezse sonsuza dek "güncel"miş gibi görünür kalır.
        // Faz 4 denetiminde bulundu: işlem 90 günün dışına çıktığında segment hiç değişmiyordu.
        var staleActiveSegments = await segmentsQuery
            .Where(s => s.IsActive && !activeCustomerIds.Contains(s.CustomerId))
            .ToListAsync(cancellationToken);
        foreach (var stale in staleActiveSegments)
            stale.Deactivate();

        if (activitySummaries.Count > 0)
        {
            var rfmInputs = activitySummaries
                .Select(a => new RfmCalculator.CustomerActivity(a.CustomerId, a.LastTransactionUtc, a.Count, a.Total))
                .ToList();
            var rfmScores = RfmCalculator.CalculateScores(rfmInputs, referenceUtc).ToDictionary(s => s.CustomerId);

            // Baskın kategori: pencere içinde en çok harcanan kategori (tutar bazlı).
            var categoryTotals = await transactionsQuery
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

            // Segment.Propensity (yeni segment kaydı) tek doğruluk kaynağı olsa da,
            // Customer.CurrentPropensity (CustomerSummary.propensity ile dışarı veriliyor —
            // müşteri listesi/Dashboard bunu okuyor) senkronize edilmezse sonsuza dek
            // varsayılan Neutral'da kalır. Bulundu ve düzeltildi: bu satır hiç yoktu.
            var customers = await customersQuery
                .Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            foreach (var customerId in customerIds)
            {
                // Müşteri bu banka scope'unda değilse (BankAdmin başka bir bankanın işlemini
                // hiç görmez zaten, ama savunma amaçlı) sessizce atlanır.
                if (!customers.TryGetValue(customerId, out var customer))
                    continue;

                var rfm = rfmScores[customerId];
                var propensity = RfmCalculator.DeterminePropensity(rfm.TotalScore);
                var dominantCategory = dominantCategories.GetValueOrDefault(customerId);

                if (existingActiveByCustomer.TryGetValue(customerId, out var existing))
                    existing.Deactivate();

                // customer.BankId kullanılıyor (currentUser.ResolveBankIdForWrite() değil) —
                // SuperAdmin birden fazla bankanın müşterilerini tek seferde yeniden hesaplarken
                // her segmentin, ait olduğu müşterinin GERÇEK bankasına damgalanması gerekir.
                dbContext.Segments.Add(Segment.Create(
                    customer.BankId, customerId, rfm.RecencyScore, rfm.FrequencyScore, rfm.MonetaryScore, propensity, dominantCategory));

                customer.AssignPropensity(propensity);

                await auditLogRepository.InsertAsync(
                    AuditLog.Create(customer.BankId, customerId, AuditActionType.SegmentAssigned,
                        $"{propensity} / baskın kategori: {dominantCategory}"),
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RecalculateSegmentsResult(activeCustomerIds.Count, totalCustomers - activeCustomerIds.Count);
    }

    public async Task<SegmentSummary?> GetActiveSegmentAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var segment = await dbContext.Segments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive, cancellationToken);

        return segment is null || (!currentUser.IsSuperAdmin && segment.BankId != currentUser.BankId)
            ? null
            : new SegmentSummary(
                segment.CustomerId, segment.RecencyScore, segment.FrequencyScore, segment.MonetaryScore,
                segment.GetTotalScore(), segment.Propensity, segment.DominantCategory, segment.CalculatedAtUtc);
    }
}
