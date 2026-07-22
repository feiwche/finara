using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record AuditLogFilter(
    Guid? CustomerId = null,
    AuditActionType? ActionType = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public record AuditLogSummary(
    Guid Id,
    Guid? CustomerId,
    string? CustomerName,
    AuditActionType ActionType,
    string? Details,
    Guid? PerformedByUserId,
    DateTime OccurredAtUtc);
