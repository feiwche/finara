using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IUserPreferencesService
{
    Task<UserPreferencesDto?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserPreferencesDto?> UpdatePreferencesAsync(Guid userId, UpdateUserPreferencesRequest request, CancellationToken cancellationToken = default);
}
