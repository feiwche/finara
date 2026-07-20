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
