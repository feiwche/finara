using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserSummary>> GetAdminsAsync(CancellationToken cancellationToken = default);

    Task<AdminUserSummary> CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken = default);

    // currentUserId, isteği yapan admin'in kendi hesabını pasifleştirmesini engellemek için
    // (kendini kilitleyip dışarıda kalması bir bug değil ama tam bir kullanıcı hatası olurdu).
    Task<AdminUserSummary?> DeactivateAdminAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default);
}
