using System.Reflection;
using AwesomeAssertions;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Common;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using NSubstitute;
using OtpNet;

namespace Finara.Infrastructure.Tests;

public class AuthServiceTests : AuthServiceTestBase
{
    // RegisterAsync gerçek bir güvenlik açığını düzeltmişti (bkz. AuthService'teki yorum):
    // kontrol eklenmeden önce auth'suz herkes User.Create'in varsayılan rolü (SuperAdmin)
    // sayesinde tam yetkili bir hesap açabiliyordu. Bu testler o düzeltmeyi kilitliyor.
    [Fact]
    public async Task RegisterAsync_UsersTablosuBosken_HesapOlusturur()
    {
        await Sut.RegisterAsync(new RegisterRequest("ilk@example.com", "Password123"));

        DbContext.Users.Single().Email.Should().Be("ilk@example.com");
    }

    [Fact]
    public async Task RegisterAsync_UsersTablosuBosken_SuperAdminRoluyleOlusturur()
    {
        await Sut.RegisterAsync(new RegisterRequest("ilk@example.com", "Password123"));

        DbContext.Users.Single().Role.Should().Be(UserRole.SuperAdmin);
    }

    [Fact]
    public async Task RegisterAsync_HerhangiBirKullaniciVarsa_KayitKapaliHatasiFirlatir()
    {
        CreateUser("mevcut@example.com");

        var act = () => Sut.RegisterAsync(new RegisterRequest("yeni@example.com", "Password123"));

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Code.Should().Be(ErrorCodes.AuthRegistrationClosed);
    }

    [Fact]
    public async Task RegisterAsync_HerhangiBirKullaniciVarsa_IkinciKullaniciEklenmez()
    {
        CreateUser("mevcut@example.com");

        var act = () => Sut.RegisterAsync(new RegisterRequest("yeni@example.com", "Password123"));

        await act.Should().ThrowAsync<DomainException>();
        DbContext.Users.Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterAsync_ZayifSifre_ArgumentExceptionFirlatirVeKullaniciEklenmez()
    {
        var act = () => Sut.RegisterAsync(new RegisterRequest("ilk@example.com", "kisa1"));

        await act.Should().ThrowAsync<ArgumentException>();
        DbContext.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_GecerliBilgilerle_TokenDoner()
    {
        CreateUser("user@example.com", "Password123");

        var result = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));

        result.TwoFactorRequired.Should().BeFalse();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_YanlisSifre_UnauthorizedFirlatir()
    {
        CreateUser("user@example.com", "Password123");

        var act = () => Sut.LoginAsync(new LoginRequest("user@example.com", "YanlisSifre1"));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task LoginAsync_PasifHesap_UnauthorizedFirlatir()
    {
        CreateUser("user@example.com", "Password123", isActive: false);

        var act = () => Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task LoginAsync_2FAAcikken_MeydanOkumaTokenDoner()
    {
        var user = CreateUser("user@example.com", "Password123");
        await EnableTwoFactorAsync(user);

        var result = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));

        result.TwoFactorRequired.Should().BeTrue();
        result.ChallengeToken.Should().NotBeNullOrEmpty();
        result.AccessToken.Should().BeNull();
    }

    [Fact]
    public async Task DemoLoginAsync_DemoHesabiVarken_TokenDoner()
    {
        CreateDemoUser();

        var result = await Sut.DemoLoginAsync();

        result.TwoFactorRequired.Should().BeFalse();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DemoLoginAsync_DemoHesabiYokken_InvalidOperationFirlatir()
    {
        var act = () => Sut.DemoLoginAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_GecerliKodla_TokenDoner()
    {
        var user = CreateUser("user@example.com", "Password123");
        var secret = await EnableTwoFactorAsync(user);
        var login = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));
        // EnableTwoFactorAsync (ConfirmSetupAsync üzerinden) o anki zaman adımını zaten
        // tüketmişti — replay koruması (bkz. TwoFactorService.VerifyCodeAsync) aynı adımın
        // kodunu ikinci kez reddediyor, bu yüzden burada FARKLI bir adımın (30sn sonrası)
        // kodu kullanılıyor.
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow.AddSeconds(30));

        var result = await Sut.VerifyTwoFactorAsync(new VerifyTwoFactorRequest(login.ChallengeToken!, code));

        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_GecersizKodla_UnauthorizedFirlatir()
    {
        var user = CreateUser("user@example.com", "Password123");
        await EnableTwoFactorAsync(user);
        var login = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));

        var act = () => Sut.VerifyTwoFactorAsync(new VerifyTwoFactorRequest(login.ChallengeToken!, "000000"));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RefreshAsync_GecerliTokenla_RotasyonYapar()
    {
        CreateUser("user@example.com", "Password123");
        var login = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));

        var refreshed = await Sut.RefreshAsync(new RefreshRequest(login.RefreshToken!));

        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);
        var act = () => Sut.RefreshAsync(new RefreshRequest(login.RefreshToken!));
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RevokeAsync_GecerliTokenda_IptalEder()
    {
        CreateUser("user@example.com", "Password123");
        var login = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));

        await Sut.RevokeAsync(new RevokeRequest(login.RefreshToken!));

        var act = () => Sut.RefreshAsync(new RefreshRequest(login.RefreshToken!));
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RevokeAsync_ZatenIptalliTokenda_NoOpYapar()
    {
        CreateUser("user@example.com", "Password123");
        var login = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));
        await Sut.RevokeAsync(new RevokeRequest(login.RefreshToken!));

        var act = () => Sut.RevokeAsync(new RevokeRequest(login.RefreshToken!));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAllAsync_TumTokenlariIptalEder()
    {
        var user = CreateUser("user@example.com", "Password123");
        var login1 = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));
        var login2 = await Sut.LoginAsync(new LoginRequest("user@example.com", "Password123"));

        await Sut.RevokeAllAsync(user.Id);

        var act1 = () => Sut.RefreshAsync(new RefreshRequest(login1.RefreshToken!));
        var act2 = () => Sut.RefreshAsync(new RefreshRequest(login2.RefreshToken!));
        await act1.Should().ThrowAsync<DomainException>();
        await act2.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RevokeAllAsync_DemoHesabinda_InvalidOperationFirlatir()
    {
        var act = () => Sut.RevokeAllAsync(DemoAccount.Id);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ForgotPasswordAsync_IzinsizOrigin_ArgumentExceptionFirlatir()
    {
        CreateUser("user@example.com", "Password123");

        var act = () => Sut.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com", "https://kotu-adres.com"));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ForgotPasswordAsync_BilinmeyenEposta_SessizceDoner()
    {
        var act = () => Sut.ForgotPasswordAsync(new ForgotPasswordRequest("yok@example.com", "http://localhost:4200"));

        await act.Should().NotThrowAsync();
        await EmailSender.DidNotReceive().SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UiLanguage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_GecerliTokenla_SifreyiDegistirir()
    {
        CreateUser("user@example.com", "Password123");
        await Sut.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com", "http://localhost:4200"));
        var rawToken = CaptureRawResetToken();

        await Sut.ResetPasswordAsync(new ResetPasswordRequest(rawToken, "YeniSifre456"));

        var login = await Sut.LoginAsync(new LoginRequest("user@example.com", "YeniSifre456"));
        login.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResetPasswordAsync_AyniTokenIkinciKez_UnauthorizedFirlatir()
    {
        CreateUser("user@example.com", "Password123");
        await Sut.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com", "http://localhost:4200"));
        var rawToken = CaptureRawResetToken();
        await Sut.ResetPasswordAsync(new ResetPasswordRequest(rawToken, "YeniSifre456"));

        var act = () => Sut.ResetPasswordAsync(new ResetPasswordRequest(rawToken, "BaskaSifre789"));

        await act.Should().ThrowAsync<DomainException>();
    }

    // ForgotPasswordAsync ham token'ı sadece e-postaya (mock/sahte gönderici) yazıyor, DB'ye
    // sadece hash'i — testte ham token'a ulaşmak için gönderici çağrısını yakalayıp resetUrl'den
    // ayrıştırıyoruz (gerçek kullanıcı akışıyla birebir aynı yol, kısayol değil).
    private string CaptureRawResetToken()
    {
        var call = EmailSender.ReceivedCalls()
            .Last(c => c.GetMethodInfo().Name == nameof(IAdminEmailSender.SendPasswordResetEmailAsync));
        var resetUrl = (string)call.GetArguments()[1]!;
        var query = new Uri(resetUrl).Query;
        return Uri.UnescapeDataString(query.Split("token=")[1]);
    }

    private async Task<string> EnableTwoFactorAsync(User user)
    {
        var setup = await TwoFactorService.BeginSetupAsync(user.Id);
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();
        await TwoFactorService.ConfirmSetupAsync(user.Id, code);
        return setup.Secret;
    }

    private void CreateDemoUser()
    {
        var bank = Bank.Create("Test Bankası", "TEST");
        DbContext.Banks.Add(bank);

        var user = User.Create(DemoAccount.Email, "unused-hash", UserRole.Analyst, bank.Id);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(user, DemoAccount.Id);
        DbContext.Users.Add(user);
        DbContext.SaveChanges();
    }
}
