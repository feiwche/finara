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
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Finara.Infrastructure.Services;

public class AuthService(
    FinaraDbContext dbContext,
    IConfiguration configuration,
    IAdminEmailSender emailSender,
    ILogger<AuthService> logger)
    : IAuthService
{
    // client (4200) ve backoffice (4300) aynı User hesaplarıyla ayrı origin'lerden giriş
    // yapıyor — CORS'taki izinli origin listesiyle aynı gerekçe (bkz. Program.cs AddCors),
    // sıfırlama bağlantısının keyfi bir adrese yönlendirilmesini (open redirect/e-posta
    // enjeksiyonu) önlemek için burada da doğrulanıyor. `Auth:AllowedPasswordResetOrigins`
    // appsettings/user-secrets'tan (virgülle ayrılmış) okunuyor — üretimde gerçek domain(ler)
    // koda dokunmadan eklenebilsin diye; hiç yapılandırılmazsa dev origin'lerine düşülüyor.
    private string[] AllowedResetUrlBases =>
        configuration["Auth:AllowedPasswordResetOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? ["http://localhost:4200", "http://localhost:4300"];

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

        // Hesap var mı/pasif mi/şifre yanlış mı — hiçbiri ayrı mesajla dışarı sızmıyor,
        // hepsi aynı jenerik 401'e düşüyor (hesap varlığı/durumu oracle'ı önlemek için).
        // Verify her zaman çağrılıyor (user null değilse) ki IsActive kontrolü zamanlama
        // farkıyla ayrı bir oracle oluşturmasın.
        var passwordValid = user is not null && PasswordHasher.Verify(request.Password, user.PasswordHash);
        if (user is null || !passwordValid || !user.IsActive)
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

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

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (!AllowedResetUrlBases.Contains(request.ResetUrlBase, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Geçersiz yönlendirme adresi.", nameof(request.ResetUrlBase));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // Hesap yoksa/pasifse sessizce dönülüyor — endpoint her durumda aynı jenerik
        // mesajı gösterir, böylece bir e-postanın sistemde kayıtlı olup olmadığı
        // (hesap varlığı oracle'ı) dışarı sızmaz.
        if (user is null || !user.IsActive)
            return;

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var resetToken = PasswordResetToken.Create(user.Id, RefreshTokenHasher.Hash(rawToken), DateTime.UtcNow.AddMinutes(30));
        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{request.ResetUrlBase}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        try
        {
            await emailSender.SendPasswordResetEmailAsync(user.Email, resetUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Bilinçli olarak yutuluyor — bir SMTP hatasının bu uçta 500'e düşmesi, "hesap
            // var + e-posta başarısız" durumunu "hesap yok" durumundan (sessizce 200 dönen)
            // ayırt edilebilir kılardı; bu da endpoint'in "hesap varlığını sızdırma" niyetini
            // (bkz. yukarıdaki yorum) zamanlama/durum koduyla dolaylı olarak bozardı.
            logger.LogWarning(ex, "Şifre sıfırlama e-postası gönderilemedi (kullanıcı: {UserId})", user.Id);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        PasswordPolicy.Validate(request.NewPassword);

        var tokenHash = RefreshTokenHasher.Hash(request.Token);
        var now = DateTime.UtcNow;

        // Önce OKUYUP sonra AYRI bir SaveChanges ile "kullanıldı" işaretlemek TOCTOU'ya
        // açıktı — aynı token'la eşzamanlı iki istek ikisi de IsActive=true görüp ikisi de
        // şifreyi değiştirebiliyordu (kanıtlandı, hiçbir concurrency token yoktu). Bunun
        // yerine "kullanılmamış mı" kontrolü ile "kullanıldı" işaretlemesi TEK bir atomik
        // UPDATE'te birleştiriliyor — sadece gerçekten satırı güncelleyen istek (rowsAffected=1)
        // devam eder, diğeri (rowsAffected=0) 401 alır.
        var rowsAffected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE PasswordResetTokens SET UsedAtUtc = {now} WHERE TokenHash = {tokenHash} AND UsedAtUtc IS NULL AND ExpiresAtUtc > {now}",
            cancellationToken);
        if (rowsAffected == 0)
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş sıfırlama bağlantısı.");

        var resetToken = await dbContext.PasswordResetTokens.AsNoTracking().FirstAsync(t => t.TokenHash == tokenHash, cancellationToken);
        var user = await dbContext.Users.FirstAsync(u => u.Id == resetToken.UserId, cancellationToken);
        user.SetPasswordHash(PasswordHasher.Hash(request.NewPassword));

        // Kullanıcının varsa BAŞKA aktif sıfırlama bağlantıları da (ör. "linki gönder"e
        // birden fazla tıklanmışsa) geçersiz kılınıyor — biri kullanılınca diğerleri süresi
        // dolana kadar (30 dk) hâlâ geçerli kalıp şifreyi tekrar değiştirebilirdi.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE PasswordResetTokens SET UsedAtUtc = {now} WHERE UserId = {user.Id} AND UsedAtUtc IS NULL",
            cancellationToken);

        // Şifre sıfırlandığında tüm oturumlar da iptal edilmeli — token'ı ele geçiren biri
        // varsa (asıl sıfırlama nedeni bu olabilir), sıfırlama sonrası her yerden yeniden
        // giriş zorunlu kılınıyor.
        var activeRefreshTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in activeRefreshTokens)
            refreshToken.Revoke();

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
