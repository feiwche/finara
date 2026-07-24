using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record SegmentSummary(
    Guid CustomerId,
    int RecencyScore,
    int FrequencyScore,
    int MonetaryScore,
    int TotalScore,
    SpendingPropensity Propensity,
    TransactionCategory? DominantCategory,
    DateTime CalculatedAtUtc,
    string? AiCommentary);

public record RecalculateSegmentsResult(int CustomersProcessed, int CustomersSkipped);
