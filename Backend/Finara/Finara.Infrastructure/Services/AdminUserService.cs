using System.Security.Cryptography;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Common;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Finara.Infrastructure.Services;

public class AdminUserService(
    FinaraDbContext dbContext,
    ICurrentUserContext currentUser,
    IAuditLogRepository auditLogRepository,
    IAdminEmailSender emailSender,
    IConfiguration configuration,
    ILogger<AdminUserService> logger)
    : IAdminUserService
{
    // AuthService.AllowedResetUrlBases ile aynı gerekçe/kaynak — davet linki de client
    // (4200) ya da backoffice'ten (4300) gönderilebiliyor, backend tek bir sabit domain
    // varsayamıyor; aynı allowlist açık yönlendirmeyi burada da önlüyor.
    private string[] AllowedInviteUrlBases =>
        configuration["Auth:AllowedPasswordResetOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? ["http://localhost:4200", "http://localhost:4300"];

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
                $"{user.Email} ({(role == UserRole.SuperAdmin ? "SuperAdmin" : "BankAdmin")})", currentUser.UserId),
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
            AuditLog.Create(user.BankId ?? DefaultBank.Id, null, AuditActionType.AdminDeactivated, user.Email, currentUserId),
            cancellationToken);

        return ToSummary(user);
    }

    public async Task<IReadOnlyList<InvitationSummary>> GetInvitationsAsync(CancellationToken cancellationToken = default)
    {
        var query = dbContext.UserInvitations.AsNoTracking().AsQueryable();
        if (!currentUser.IsSuperAdmin)
            query = query.Where(i => i.BankId == currentUser.BankId);

        var invitations = await query.OrderByDescending(i => i.CreatedAtUtc).ToListAsync(cancellationToken);
        return invitations.Select(ToSummary).ToList();
    }

    public async Task<InvitationSummary> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default)
    {
        if (!AllowedInviteUrlBases.Contains(request.InviteUrlBase, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Geçersiz yönlendirme adresi.", nameof(request.InviteUrlBase));

        // Rol/BankId kuralları CreateAdminAsync ile birebir aynı — BankAdmin sadece kendi
        // bankası için BankAdmin daveti çıkarabilir, SuperAdmin serbest seçebilir.
        UserRole role;
        Guid? bankId;
        if (currentUser.IsSuperAdmin)
        {
            role = request.Role ?? UserRole.BankAdmin;
            if (role == UserRole.BankAdmin)
            {
                if (request.BankId is null)
                    throw new ArgumentException("BankAdmin daveti için bir banka seçilmeli.", nameof(request.BankId));
                bankId = request.BankId;
            }
            else
            {
                bankId = null;
            }
        }
        else
        {
            role = UserRole.BankAdmin;
            bankId = currentUser.BankId;
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var userExists = await dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (userExists)
            throw new InvalidOperationException($"'{request.Email}' e-posta adresiyle kayıtlı bir kullanıcı zaten var.");

        var hasPendingInvitation = await dbContext.UserInvitations.AnyAsync(
            i => i.Email == normalizedEmail && i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > DateTime.UtcNow,
            cancellationToken);
        if (hasPendingInvitation)
            throw new InvalidOperationException($"'{request.Email}' e-posta adresine zaten bekleyen bir davet var.");

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var invitation = UserInvitation.Create(
            normalizedEmail, role, bankId, RefreshTokenHasher.Hash(rawToken), DateTime.UtcNow.AddHours(24), currentUser.UserId!.Value);

        dbContext.UserInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(bankId ?? DefaultBank.Id, null, AuditActionType.AdminInvited,
                $"{invitation.Email} ({(role == UserRole.SuperAdmin ? "SuperAdmin" : "BankAdmin")})", currentUser.UserId),
            cancellationToken);

        var acceptUrl = $"{request.InviteUrlBase}/accept-invitation?token={Uri.EscapeDataString(rawToken)}";
        try
        {
            await emailSender.SendInvitationEmailAsync(invitation.Email, acceptUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ForgotPasswordAsync'teki yutma deseninden farklı olarak burada bir "hesap
            // varlığı" oracle riski yok (davet oluşturan zaten hedef e-postayı biliyor) —
            // yine de admin akışını bir SMTP hatasıyla kesmemek için loglanıp yutuluyor;
            // davet DB'de bekliyor kalır, admin gerekirse iptal edip tekrar davet çıkarabilir.
            logger.LogWarning(ex, "Davet e-postası gönderilemedi (davet: {InvitationId})", invitation.Id);
        }

        return ToSummary(invitation);
    }

    public async Task<InvitationSummary?> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await dbContext.UserInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        // Başka bankanın davetine erişim denemesi de "bulunamadı" — AdminUserService'teki
        // diğer metodlarla aynı bilgi-sızdırmama deseni.
        if (invitation is null || (!currentUser.IsSuperAdmin && invitation.BankId != currentUser.BankId))
            return null;

        invitation.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(invitation.BankId ?? DefaultBank.Id, null, AuditActionType.AdminInvitationRevoked, invitation.Email, currentUser.UserId),
            cancellationToken);

        return ToSummary(invitation);
    }

    private static InvitationSummary ToSummary(UserInvitation invitation) => new(
        invitation.Id, invitation.Email, invitation.Role, invitation.BankId,
        invitation.ExpiresAtUtc, invitation.AcceptedAtUtc, invitation.RevokedAtUtc,
        invitation.CreatedAtUtc, invitation.InvitedByUserId);

    private static AdminUserSummary ToSummary(User user) =>
        new(user.Id, user.Email, user.Role, user.BankId, user.IsActive, user.CreatedAtUtc);
}
