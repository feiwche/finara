using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    Task RevokeAsync(RevokeRequest request, CancellationToken cancellationToken = default);

    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<InvitationPreview?> GetInvitationPreviewAsync(string token, CancellationToken cancellationToken = default);

    Task AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken = default);
}
