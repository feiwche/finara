using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

// Role/BankId sadece SuperAdmin çağırdığında anlamlı — BankAdmin çağırırsa ikisi de
// yoksayılır, oluşturulan hesap her zaman BankAdmin + çağıranın kendi bankası olur
// (bkz. AdminUserService.CreateAdminAsync).
public record CreateAdminRequest(string Email, string Password, UserRole? Role = null, Guid? BankId = null);

public record AdminUserSummary(
    Guid Id,
    string Email,
    UserRole Role,
    Guid? BankId,
    bool IsActive,
    DateTime CreatedAtUtc);

// Role/BankId burada da sadece SuperAdmin çağırdığında anlamlı — CreateAdminRequest ile
// aynı kural (bkz. AdminUserService.CreateInvitationAsync).
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
