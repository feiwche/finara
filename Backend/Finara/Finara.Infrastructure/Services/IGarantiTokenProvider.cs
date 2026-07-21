namespace Finara.Infrastructure.Services;

public interface IGarantiTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
