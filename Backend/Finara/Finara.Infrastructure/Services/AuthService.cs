using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Application.Security;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Finara.Infrastructure.Services;

public class AuthService(FinaraDbContext dbContext, IConfiguration configuration) : IAuthService
{
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        PasswordPolicy.Validate(request.Password);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"'{request.Email}' e-posta adresiyle kayıtlı bir kullanıcı zaten var.");

        var user = User.Create(request.Email, PasswordHasher.Hash(request.Password));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Bu hesap pasifleştirilmiş.");

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = RefreshTokenHasher.Hash(request.RefreshToken);
        var existing = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenHash, cancellationToken);
        if (existing is null || !existing.IsActive)
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");

        var user = await dbContext.Users.FirstAsync(u => u.Id == existing.UserId, cancellationToken);

        // Rotasyon: eskisini iptal et, yenisini üret — çalınmış bir refresh token'ın
        // tekrar tekrar kullanılabilmesini engeller.
        // existing.Revoke() burada henüz kaydedilmiyor — IssueTokensAsync sonundaki
        // SaveChangesAsync, aynı DbContext'te izlenen bu değişikliği de birlikte yazıyor.
        existing.Revoke();
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task RevokeAsync(RevokeRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = RefreshTokenHasher.Hash(request.RefreshToken);
        var existing = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenHash, cancellationToken);
        if (existing is null || !existing.IsActive)
            return; // zaten geçersiz/iptal edilmiş — sessizce no-op (idempotent logout)

        existing.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResult> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key yapılandırılmamış (user-secrets kontrol edin).");
        var accessTokenMinutes = int.Parse(jwtSection["AccessTokenMinutes"] ?? "15");
        var refreshTokenDays = int.Parse(jwtSection["RefreshTokenDays"] ?? "7");

        var accessTokenExpires = DateTime.UtcNow.AddMinutes(accessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // SuperAdmin için eklenmiyor (BankId null) — tüm bankaları görür. BankAdmin
        // için ekleniyor, CurrentUserContext bunu okuyup servislerde bank-scope uyguluyor.
        if (user.BankId.HasValue)
            claims.Add(new Claim(JwtClaimNames.BankId, user.BankId.Value.ToString()));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: accessTokenExpires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // İstemciye ham (rastgele) değer döner, DB'ye sadece hash'i yazılır — çalınan bir
        // DB dump'ından oturumların ele geçirilememesi için (bkz. RefreshTokenHasher).
        var refreshTokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshTokenExpires = DateTime.UtcNow.AddDays(refreshTokenDays);
        var refreshToken = RefreshToken.Create(user.Id, RefreshTokenHasher.Hash(refreshTokenValue), refreshTokenExpires);

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResult(accessToken, accessTokenExpires, refreshTokenValue, refreshTokenExpires);
    }
}
