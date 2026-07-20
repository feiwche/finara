using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record CreateAdminRequest(string Email, string Password);

public record AdminUserSummary(
    Guid Id,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAtUtc);
