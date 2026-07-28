using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Common;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using QRCoder;

namespace Finara.Infrastructure.Services;

public class TwoFactorService : ITwoFactorService
{
    private const string ProtectorPurpose = "Finara.TwoFactorSecrets";
    private readonly FinaraDbContext dbContext;
    private readonly IDataProtector protector;

    public TwoFactorService(FinaraDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        this.dbContext = dbContext;
        protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    public async Task<TwoFactorSetupResult> BeginSetupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Paylaşılan demo hesabında 2FA kurulumu engellenir — aksi halde bir ziyaretçi
        // demo hesabını 2FA'yla kilitleyip diğer herkesi (yeni demo-login denemelerini
        // etkilemese de) karışıklığa sokabilirdi.
        if (userId == DemoAccount.Id)
            throw new DomainException(ErrorCodes.AuthDemoAccountRestricted, "Demo hesabında bu işlem kullanılamaz.");

        var user = await dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        // Etkinken sessizce yeni sır üretilmesini engelliyoruz — bir saldırgan çalıntı bir
        // access token'la mevcut kurulumu sessizce değiştirip kendi cihazını ekleyemesin diye.
        // Önce DisableAsync ile (mevcut bir kodla) devre dışı bırakılması gerekiyor.
        if (user.TwoFactorEnabled)
            throw new DomainException(ErrorCodes.UserTwoFactorAlreadyEnabled, "İki faktörlü doğrulama zaten etkin — önce devre dışı bırakın.");

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);

        user.SetPendingTwoFactorSecret(protector.Protect(base32Secret));
        await dbContext.SaveChangesAsync(cancellationToken);

        var otpAuthUri = $"otpauth://totp/Finara:{Uri.EscapeDataString(user.Email)}?secret={base32Secret}&issuer=Finara&digits=6&period=30";

        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        var qrCodePng = new PngByteQRCode(qrCodeData).GetGraphic(10);
        var qrCodeDataUri = $"data:image/png;base64,{Convert.ToBase64String(qrCodePng)}";

        return new TwoFactorSetupResult(base32Secret, otpAuthUri, qrCodeDataUri);
    }

    public async Task ConfirmSetupAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        if (userId == DemoAccount.Id)
            throw new DomainException(ErrorCodes.AuthDemoAccountRestricted, "Demo hesabında bu işlem kullanılamaz.");

        var user = await dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        if (!await VerifyCodeAsync(user, code, cancellationToken))
            throw new DomainException(ErrorCodes.AuthTwoFactorInvalidCode, "Kod geçersiz veya süresi dolmuş.", 401);

        user.ConfirmTwoFactor();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        if (userId == DemoAccount.Id)
            throw new DomainException(ErrorCodes.AuthDemoAccountRestricted, "Demo hesabında bu işlem kullanılamaz.");

        var user = await dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        if (!await VerifyCodeAsync(user, code, cancellationToken))
            throw new DomainException(ErrorCodes.AuthTwoFactorInvalidCode, "Kod geçersiz veya süresi dolmuş.", 401);

        user.DisableTwoFactor();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> VerifyCodeAsync(User user, string code, CancellationToken cancellationToken = default)
    {
        if (user.TwoFactorSecretEncrypted is null || string.IsNullOrWhiteSpace(code))
            return Task.FromResult(false);

        var base32Secret = protector.Unprotect(user.TwoFactorSecretEncrypted);
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));

        // ±1 adım (±30sn) tolerans — Google Authenticator'ın kendi varsayılan saat kayması
        // toleransıyla aynı, farklı bir cihaz saatinin birkaç saniye kaymasını tolere eder.
        var isValid = totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
        return Task.FromResult(isValid);
    }
}
