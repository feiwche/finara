using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

// AgeBracket/EducationLevel/Propensity null olması IsOther=true (yani "Diğer" kovası,
// eşiğin altında kalan mikro-kohortların birleştiği grup) olduğu anlamına gelir.
public record CohortSummary(
    AgeBracket? AgeBracket,
    EducationLevel? EducationLevel,
    SpendingPropensity? Propensity,
    TransactionCategory? DominantCategory,
    int CustomerCount,
    bool IsOther);
