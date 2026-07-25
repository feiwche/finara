using Finara.Application.Contracts;
using Finara.Domain.Entities;

namespace Finara.Application.Interfaces;

public interface ITwoFactorService
{
    Task<TwoFactorSetupResult> BeginSetupAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ConfirmSetupAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    Task DisableAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    Task<bool> VerifyCodeAsync(User user, string code, CancellationToken cancellationToken = default);
}
