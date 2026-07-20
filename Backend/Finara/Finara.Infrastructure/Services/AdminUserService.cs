using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class AdminUserService(FinaraDbContext dbContext) : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserSummary>> GetAdminsAsync(CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);
        return users.Select(ToSummary).ToList();
    }

    public async Task<AdminUserSummary> CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken = default)
    {
        PasswordPolicy.Validate(request.Password);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"'{request.Email}' e-posta adresiyle kayıtlı bir kullanıcı zaten var.");

        var user = User.Create(request.Email, PasswordHasher.Hash(request.Password));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(user);
    }

    public async Task<AdminUserSummary?> DeactivateAdminAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (userId == currentUserId)
            throw new InvalidOperationException("Kendi hesabınızı pasifleştiremezsiniz.");

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return null;

        user.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(user);
    }

    private static AdminUserSummary ToSummary(User user) =>
        new(user.Id, user.Email, user.Role, user.IsActive, user.CreatedAtUtc);
}
