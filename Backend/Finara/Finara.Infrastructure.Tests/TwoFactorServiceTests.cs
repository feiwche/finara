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

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task BeginSetupAsync_DemoHesabinda_InvalidOperationFirlatir()
    {
        var act = () => TwoFactorService.BeginSetupAsync(DemoAccount.Id);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ConfirmSetupAsync_GecerliKodla_Etkinlestirir()
    {
        var user = CreateUser();
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();

        await TwoFactorService.ConfirmSetupAsync(user.Id, code);

        var reloaded = await ReloadUserAsync(user.Id);
        reloaded.TwoFactorEnabled.Should().BeTrue();
    }

    // Denetimde bulunan bug: TOTP replay koruması yoktu — aynı kod, ±1 adımlık tolerans
    // penceresi içinde tekrar tekrar gönderilip kabul edilebiliyordu (ör. ağdan yakalanmış
    // bir kodun kısa sürede tekrar kullanılması). ConfirmSetupAsync zaten VerifyCodeAsync'i
    // içeride bir kez çağırıp o zaman adımını "kullanıldı" işaretlediği için, AYNI kod
    // ikinci kez gönderildiğinde artık reddedilmesi gerekiyor.
    [Fact]
    public async Task VerifyCodeAsync_AyniKodIkinciKezGonderilirse_ReplayOlarakReddedilir()
    {
        var user = CreateUser();
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();
        await TwoFactorService.ConfirmSetupAsync(user.Id, code);

        var replayed = await TwoFactorService.VerifyCodeAsync(await ReloadUserAsync(user.Id), code);

        replayed.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmSetupAsync_GecersizKodla_UnauthorizedFirlatir()
    {
        var user = CreateUser();
        await TwoFactorService.BeginSetupAsync(user.Id);

        var act = () => TwoFactorService.ConfirmSetupAsync(user.Id, "000000");

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ConfirmSetupAsync_DemoHesabinda_InvalidOperationFirlatir()
    {
        var act = () => TwoFactorService.ConfirmSetupAsync(DemoAccount.Id, "123456");

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DisableAsync_GecerliKodla_DevreDisiBirakir()
    {
        var user = CreateUser();
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var totp = new Totp(Base32Encoding.ToBytes(setup.Secret));
        var code = totp.ComputeTotp();
        await TwoFactorService.ConfirmSetupAsync(user.Id, code);

        // Replay koruması artık ConfirmSetupAsync'in tükettiği zaman adımının aynısını
        // reddediyor — burada kullanıcının gerçekte yaptığı gibi (30sn sonra üretilen) FARKLI
        // bir zaman adımının kodu kullanılıyor, aynı kodun tekrarı değil.
        var disableCode = totp.ComputeTotp(DateTime.UtcNow.AddSeconds(30));

        await TwoFactorService.DisableAsync(user.Id, disableCode);

        var reloaded = await ReloadUserAsync(user.Id);
        reloaded.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DisableAsync_DemoHesabinda_InvalidOperationFirlatir()
    {
        var act = () => TwoFactorService.DisableAsync(DemoAccount.Id, "123456");

        await act.Should().ThrowAsync<DomainException>();
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
