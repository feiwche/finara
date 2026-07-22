using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Common;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class AdminUserService(
    FinaraDbContext dbContext,
    ICurrentUserContext currentUser,
    IAuditLogRepository auditLogRepository)
    : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserSummary>> GetAdminsAsync(CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking().AsQueryable();
        if (!currentUser.IsSuperAdmin)
            query = query.Where(u => u.BankId == currentUser.BankId);

        var users = await query.OrderBy(u => u.Email).ToListAsync(cancellationToken);
        return users.Select(ToSummary).ToList();
    }

    public async Task<AdminUserSummary> CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken = default)
    {
        PasswordPolicy.Validate(request.Password);

        UserRole role;
        Guid? bankId;
        if (currentUser.IsSuperAdmin)
        {
            role = request.Role ?? UserRole.BankAdmin;
            if (role == UserRole.BankAdmin)
            {
                if (request.BankId is null)
                    throw new ArgumentException("BankAdmin oluşturmak için bir banka seçilmeli.", nameof(request.BankId));
                bankId = request.BankId;
            }
            else
            {
                bankId = null;
            }
        }
        else
        {
            // BankAdmin sadece kendi bankası için BankAdmin oluşturabilir — istekte
            // farklı bir Role/BankId gelse bile yoksayılır (başka bankaya yönetici
            // atayamaz, SuperAdmin oluşturamaz).
            role = UserRole.BankAdmin;
            bankId = currentUser.BankId;
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"'{request.Email}' e-posta adresiyle kayıtlı bir kullanıcı zaten var.");

        var user = User.Create(request.Email, PasswordHasher.Hash(request.Password), role, bankId);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        // CustomerId=null — bu olay hiçbir müşteriyle ilgili değil (bkz. AuditLog.CustomerId
        // XML yorumu). Kaydın BankId'si YENİ kullanıcının bankasına damgalanıyor (currentUser'ın
        // değil) — bir SuperAdmin başka bir banka için BankAdmin oluşturursa, o bankanın kendi
        // Denetim Kaydı'nda görünmesi gerekiyor. Hedef de SuperAdmin ise (bankId=null) bağlı
        // olunacak banka yok, DefaultBank'a düşülüyor (CurrentUserContext.ResolveBankIdForWrite
        // ile aynı kural, ama currentUser değil YENİ kullanıcı için uygulanıyor).
        await auditLogRepository.InsertAsync(
            AuditLog.Create(bankId ?? DefaultBank.Id, null, AuditActionType.AdminCreated,
                $"{user.Email} ({(role == UserRole.SuperAdmin ? "SuperAdmin" : "BankAdmin")})"),
            cancellationToken);

        return ToSummary(user);
    }

    public async Task<AdminUserSummary?> DeactivateAdminAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (userId == currentUserId)
            throw new InvalidOperationException("Kendi hesabınızı pasifleştiremezsiniz.");

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        // Başka bankanın yöneticisine erişim denemesi de "bulunamadı" — o hesabın
        // başka bir bankada var olduğunu sızdırmaz.
        if (user is null || (!currentUser.IsSuperAdmin && user.BankId != currentUser.BankId))
            return null;

        user.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(user.BankId ?? DefaultBank.Id, null, AuditActionType.AdminDeactivated, user.Email),
            cancellationToken);

        return ToSummary(user);
    }

    private static AdminUserSummary ToSummary(User user) =>
        new(user.Id, user.Email, user.Role, user.BankId, user.IsActive, user.CreatedAtUtc);
}
