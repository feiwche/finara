using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserSummary>> GetAdminsAsync(CancellationToken cancellationToken = default);

    Task<AdminUserSummary> CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken = default);

    // currentUserId, isteği yapan admin'in kendi hesabını pasifleştirmesini engellemek için
    // (kendini kilitleyip dışarıda kalması bir bug değil ama tam bir kullanıcı hatası olurdu).
    Task<AdminUserSummary?> DeactivateAdminAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default);

    // Hesabı tamamen pasifleştirmeden (DeactivateAdminAsync) daha ölçülü bir ara adım —
    // ör. şüpheli bir oturum fark edildiğinde, kullanıcının tüm cihazlardaki oturumlarını
    // sonlandırır ama hesabı aktif bırakır. Kendi hesabın için de çağrılabilir (zararsız,
    // zaten var olan self-servis "Tüm Cihazlardan Çıkış Yap" ile aynı etkiyi yapar) —
    // DeactivateAdminAsync'teki self-engelleme burada uygulanmıyor.
    Task<AdminUserSummary?> RevokeSessionsAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvitationSummary>> GetInvitationsAsync(CancellationToken cancellationToken = default);

    Task<InvitationSummary> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default);

    Task<InvitationSummary?> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);
}
