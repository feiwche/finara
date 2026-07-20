using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Services;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class CohortService(FinaraDbContext dbContext, ICurrentUserContext currentUser) : ICohortService
{
    public async Task<IReadOnlyList<CohortSummary>> GetCohortsAsync(CancellationToken cancellationToken = default)
    {
        // Sadece aktif segmenti olan (son 90 gün içinde işlemi bulunan) müşteriler
        // kohortlara dahil edilir — SegmentationService'in "aktif" tanımıyla tutarlı.
        var segmentsQuery = dbContext.Segments.AsNoTracking().Where(s => s.IsActive);
        if (!currentUser.IsSuperAdmin)
            segmentsQuery = segmentsQuery.Where(s => s.BankId == currentUser.BankId);

        var segments = await segmentsQuery
            .Select(s => new { s.CustomerId, s.Propensity, s.DominantCategory })
            .ToListAsync(cancellationToken);

        if (segments.Count == 0)
            return Array.Empty<CohortSummary>();

        var customerIds = segments.Select(s => s.CustomerId).ToList();
        var customers = await dbContext.Customers
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        // Customer.GetAgeBracket() DateOfBirth'ten hesaplanan bir domain metodu, SQL'e
        // çevrilemez — bu yüzden müşteriler önce belleğe alınıp burada işleniyor.
        var inputs = segments
            .Where(s => customers.ContainsKey(s.CustomerId))
            .Select(s =>
            {
                var customer = customers[s.CustomerId];
                return new CohortEngine.CustomerCohortInput(
                    s.CustomerId, customer.GetAgeBracket(), customer.EducationLevel, s.Propensity, s.DominantCategory);
            })
            .ToList();

        return CohortEngine.Compute(inputs)
            .Select(c => new CohortSummary(c.AgeBracket, c.EducationLevel, c.Propensity, c.DominantCategory, c.CustomerCount, c.IsOther))
            .ToList();
    }
}
