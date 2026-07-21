using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Finara.Infrastructure.Services;

// Standart OAuth2 client_credentials akışı (RFC 6749) — Garanti'nin geliştirici portalı
// bunu kullanıyor. Singleton kaydedilmeli (Program.cs) ki token'ı istekler arasında
// önbelleğe alabilsin; SemaphoreSlim, aynı anda birden fazla isteğin token süresi
// dolduğunda paralel yenileme yapıp gereksiz çağrı harcamasını önlüyor.
// IHttpClientFactory kullanılıyor (doğrudan HttpClient DEĞİL) — AddHttpClient<T>()'in
// typed-client kısayolu servisi Transient kaydediyor, bu da instance başına tutulan
// token önbelleğini işe yaramaz kılardı; Singleton + factory bu çakışmayı önlüyor.
public class GarantiTokenProvider(IHttpClientFactory httpClientFactory, IOptions<GarantiApiOptions> options) : IGarantiTokenProvider
{
    private readonly GarantiApiOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _expiresAtUtc;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAtUtc)
            return _cachedToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Kilidi beklerken başka bir çağrı zaten yenilemiş olabilir.
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAtUtc)
                return _cachedToken;

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret
                })
            };

            var client = httpClientFactory.CreateClient("Garanti");
            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Garanti OAuth2 token yanıtı okunamadı.");

            _cachedToken = payload.AccessToken;
            // Süre dolmadan 60 saniye önce yenilensin diye pay bırakılıyor.
            _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(payload.ExpiresIn - 60, 30));

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
