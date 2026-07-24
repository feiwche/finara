using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record CreateCampaignRequest(
    string Name,
    string Description,
    DateTime StartDateUtc,
    DateTime? EndDateUtc = null,
    string VariantId = "default",
    TransactionCategory? TargetCategory = null,
    IReadOnlyCollection<SpendingPropensity>? TargetPropensities = null,
    bool TargetsInvestmentAccountHolders = false,
    bool TargetsInternationalTransferUsers = false);

public record CampaignSummary(
    Guid Id,
    string Name,
    string Description,
    CampaignStatus Status,
    string VariantId,
    TransactionCategory? TargetCategory,
    IReadOnlyCollection<SpendingPropensity> TargetPropensities,
    bool TargetsInvestmentAccountHolders,
    bool TargetsInternationalTransferUsers,
    DateTime StartDateUtc,
    DateTime? EndDateUtc);
