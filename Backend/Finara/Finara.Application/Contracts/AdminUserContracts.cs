using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

// SuperAdmin çağırırsa Role/BankId serbestçe seçilebilir. BankAdmin çağırırsa BankId
// yoksayılır (her zaman kendi bankası) ve Role sadece BankAdmin/Analyst arasında seçilebilir
// (Analyst değilse BankAdmin'e düşer, SuperAdmin İSTEYEMEZ) — bkz. AdminUserService.CreateAdminAsync.
public record CreateAdminRequest(string Email, string Password, UserRole? Role = null, Guid? BankId = null);

public record AdminUserSummary(
    Guid Id,
    string Email,
    UserRole Role,
    Guid? BankId,
    bool IsActive,
    DateTime CreatedAtUtc);

// CreateAdminRequest ile aynı Role/BankId kuralı (bkz. AdminUserService.CreateInvitationAsync).
public record CreateInvitationRequest(string Email, string InviteUrlBase, UserRole? Role = null, Guid? BankId = null);

public record InvitationSummary(
    Guid Id,
    string Email,
    UserRole Role,
    Guid? BankId,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc,
    DateTime CreatedAtUtc,
    Guid InvitedByUserId);
