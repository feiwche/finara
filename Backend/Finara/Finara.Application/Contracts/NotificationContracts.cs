using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record DistributeOfferRequest(ConsentChannel Channel);

public record NotificationDispatchResult(bool Sent, string? SkippedReason);

public record NotificationLogSummary(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid? CampaignOfferId,
    ConsentChannel Channel,
    string Message,
    bool Sent,
    string? SkippedReason,
    DateTime SentAtUtc);

// Tüm alanlar opsiyonel — AuditLogFilter (Faz 7.2) ile aynı desen.
public record NotificationLogFilter(Guid? CustomerId, ConsentChannel? Channel, DateTime? FromUtc, DateTime? ToUtc);
