using Finara.Domain.Enums;

namespace Finara.Domain.Services;

// Saf, DB'den bağımsız hesaplama — girdi olarak müşteri popülasyonunun demografik/davranışsal
// özetini alır, yaş aralığı x eğitim durumu x eğilim boyutlarında kohortlar üretir. DB sorgusu
// CohortService'te (Infrastructure). RfmCalculator ile aynı stil: saf fonksiyon, kolay test edilir.
public static class CohortEngine
{
    // Kombinatorik patlama riski (mimar incelemesinde işaretlendi, Faz 8 planında kabul edildi):
    // bu eşiğin altındaki mikro-kohortlar tek bir "Diğer" kovasında toplanır.
    public const int DefaultMinCohortSize = 5;

    public record CustomerCohortInput(
        Guid CustomerId,
        AgeBracket AgeBracket,
        EducationLevel? EducationLevel,
        SpendingPropensity Propensity,
        TransactionCategory? DominantCategory);

    // AgeBracket/EducationLevel/Propensity null olması IsOther=true olduğunu (yani "Diğer"
    // kovası) gösterir — bu durumda üyeler ortak bir boyutu paylaşmıyor, sadece küçük
    // gruplar birleştirilmiş oluyor.
    public record Cohort(
        AgeBracket? AgeBracket,
        EducationLevel? EducationLevel,
        SpendingPropensity? Propensity,
        TransactionCategory? DominantCategory,
        int CustomerCount,
        IReadOnlyList<Guid> CustomerIds,
        bool IsOther);

    public static IReadOnlyList<Cohort> Compute(IReadOnlyList<CustomerCohortInput> inputs, int minCohortSize = DefaultMinCohortSize)
    {
        if (inputs.Count == 0)
            return Array.Empty<Cohort>();

        var groups = inputs
            .GroupBy(i => (i.AgeBracket, i.EducationLevel, i.Propensity))
            .Select(g => new Cohort(
                g.Key.AgeBracket,
                g.Key.EducationLevel,
                g.Key.Propensity,
                DominantCategoryOf(g),
                g.Count(),
                g.Select(i => i.CustomerId).ToList(),
                IsOther: false))
            .ToList();

        var cohorts = groups.Where(c => c.CustomerCount >= minCohortSize).ToList();
        var microCohorts = groups.Where(c => c.CustomerCount < minCohortSize).ToList();

        if (microCohorts.Count > 0)
        {
            var mergedIds = microCohorts.SelectMany(c => c.CustomerIds).ToList();
            var mergedInputs = inputs.Where(i => mergedIds.Contains(i.CustomerId)).ToList();

            cohorts.Add(new Cohort(
                AgeBracket: null,
                EducationLevel: null,
                Propensity: null,
                DominantCategory: DominantCategoryOf(mergedInputs),
                CustomerCount: mergedIds.Count,
                CustomerIds: mergedIds,
                IsOther: true));
        }

        // "Diğer" kovası büyüklüğüne bakılmaksızın her zaman en sonda gösterilir — o bir
        // kohort değil, kohort OLMAYAN her şeyin toplandığı yer, en üstte çıkması yanıltıcı olur.
        return cohorts.OrderBy(c => c.IsOther).ThenByDescending(c => c.CustomerCount).ToList();
    }

    private static TransactionCategory? DominantCategoryOf(IEnumerable<CustomerCohortInput> members) =>
        members
            .Where(m => m.DominantCategory.HasValue)
            .GroupBy(m => m.DominantCategory!.Value)
            .OrderByDescending(g => g.Count())
            .Select(g => (TransactionCategory?)g.Key)
            .FirstOrDefault();
}
