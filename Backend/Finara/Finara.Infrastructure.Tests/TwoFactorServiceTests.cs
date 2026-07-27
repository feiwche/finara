using AwesomeAssertions;
using Finara.Domain.Common;
using OtpNet;

namespace Finara.Infrastructure.Tests;

public class TwoFactorServiceTests : AuthServiceTestBase
{
    [Fact]
    public async Task BeginSetupAsync_YeniKullaniciIcin_SirVeQrDoner()
    {
        var user = CreateUser();

        var result = await TwoFactorService.BeginSetupAsync(user.Id);

        result.Secret.Should().NotBeNullOrEmpty();
        result.QrCodeDataUri.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task BeginSetupAsync_ZatenEtkinken_InvalidOperationFirlatir()
    {
        var user = CreateUser();
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();
        await TwoFactorService.ConfirmSetupAsync(user.Id, code);

        var act = () => TwoFactorService.BeginSetupAsync(user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task BeginSetupAsync_DemoHesabinda_InvalidOperationFirlatir()
    {
        var act = () => TwoFactorService.BeginSetupAsync(DemoAccount.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ConfirmSetupAsync_GecerliKodla_EtkinlestirirVeDogruKoduKabulEder()
    {
        var user = CreateUser();
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();

        await TwoFactorService.ConfirmSetupAsync(user.Id, code);

        var again = await TwoFactorService.VerifyCodeAsync(await ReloadUserAsync(user.Id), code);
        again.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmSetupAsync_GecersizKodla_UnauthorizedFirlatir()
    {
        var user = CreateUser();
        await TwoFactorService.BeginSetupAsync(user.Id);

        var act = () => TwoFactorService.ConfirmSetupAsync(user.Id, "000000");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ConfirmSetupAsync_DemoHesabinda_InvalidOperationFirlatir()
    {
        var act = () => TwoFactorService.ConfirmSetupAsync(DemoAccount.Id, "123456");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisableAsync_GecerliKodla_DevreDisiBirakir()
    {
        var user = CreateUser();
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();
        await TwoFactorService.ConfirmSetupAsync(user.Id, code);
        var disableCode = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();

        await TwoFactorService.DisableAsync(user.Id, disableCode);

        var reloaded = await ReloadUserAsync(user.Id);
        reloaded.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DisableAsync_DemoHesabinda_InvalidOperationFirlatir()
    {
        var act = () => TwoFactorService.DisableAsync(DemoAccount.Id, "123456");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VerifyCodeAsync_GecerliTotpKoduyla_Dogrular()
    {
        var user = CreateUser();
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();

        var isValid = await TwoFactorService.VerifyCodeAsync(await ReloadUserAsync(user.Id), code);

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyCodeAsync_GecersizKodla_Reddedilir()
    {
        var user = CreateUser();
        await TwoFactorService.BeginSetupAsync(user.Id);

        var isValid = await TwoFactorService.VerifyCodeAsync(await ReloadUserAsync(user.Id), "000000");

        isValid.Should().BeFalse();
    }

    private async Task<Finara.Domain.Entities.User> ReloadUserAsync(Guid userId) =>
        await DbContext.Users.FindAsync(userId) ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");
}
