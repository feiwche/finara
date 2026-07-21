using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record ConsentSummary(
    ConsentChannel Channel,
    bool IsGranted,
    DateTime? GrantedAtUtc,
    DateTime? RevokedAtUtc);

public record UpdateConsentRequest(ConsentChannel Channel, bool IsGranted);
