namespace Finara.Application.Contracts;

public record OfferSummary(
    Guid Id,
    Guid CampaignId,
    string CampaignName,
    Guid CustomerId,
    double ConfidenceScore,
    string Response,
    DateTime PresentedAtUtc);

public record GenerateOffersResult(int OffersCreated);
