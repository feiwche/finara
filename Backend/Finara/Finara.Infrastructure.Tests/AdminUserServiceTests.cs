using AwesomeAssertions;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Finara.Infrastructure.Tests;

// AdminUserService'in HİÇ testi yoktu (denetimde bulundu). Buradaki rol/banka atama mantığı
// bir YETKİ SINIRI: BankAdmin başka bankaya yönetici atayamamalı ve kendini/başkasını
// SuperAdmin yapamamalı. Bu kural "sessizce yanlış davranma" riski taşıyor — istekteki
// alanlar yoksayılarak uygulanıyor, yani bozulursa hiçbir şey patlamaz, sadece yetki yükselir.
public class AdminUserServiceTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly FinaraDbContext dbContext;
    private readonly ICurrentUserContext currentUser;
    private readonly AdminUserService sut;
    private readonly Guid bankAId;
    private readonly Guid bankBId;

    public AdminUserServiceTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FinaraDbContext>().UseSqlite(connection).Options;
        dbContext = new TestDbContext(options);
        dbContext.Database.EnsureCreated();

        var bankA = Bank.Create("Banka A", "BKA");
        var bankB = Bank.Create("Banka B", "BKB");
        dbContext.Banks.AddRange(bankA, bankB);
        dbContext.SaveChanges();
        bankAId = bankA.Id;
        bankBId = bankB.Id;

        currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.IsSuperAdmin.Returns(false);
        currentUser.BankId.Returns(bankAId);
        currentUser.UserId.Returns(Guid.NewGuid());

        sut = new AdminUserService(
            dbContext,
            currentUser,
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IAdminEmailSender>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            Substitute.For<IAuthService>(),
            NullLogger<AdminUserService>.Instance);
    }

    [Fact]
    public async Task CreateAdminAsync_BankAdmin_BaskaBankaIsteseBileKendiBankasinaOlusturur()
    {
        // BankAdmin, isteğe Banka B'yi yazıyor — yoksayılmalı, kendi bankasına (A) düşmeli.
        var created = await sut.CreateAdminAsync(
            new CreateAdminRequest("yeni@example.com", "Gecerli123", UserRole.BankAdmin, bankBId));

        var user = dbContext.Users.Single(u => u.Email == "yeni@example.com");
        user.BankId.Should().Be(bankAId);
        user.BankId.Should().NotBe(bankBId);
        created.Email.Should().Be("yeni@example.com");
    }

    [Fact]
    public async Task CreateAdminAsync_BankAdmin_SuperAdminOlusturamaz()
    {
        // Yetki yükseltme denemesi: istekte SuperAdmin isteniyor, BankAdmin'e düşmeli.
        await sut.CreateAdminAsync(
            new CreateAdminRequest("yukselt@example.com", "Gecerli123", UserRole.SuperAdmin, null));

        var user = dbContext.Users.Single(u => u.Email == "yukselt@example.com");
        user.Role.Should().Be(UserRole.BankAdmin);
        user.Role.Should().NotBe(UserRole.SuperAdmin);
        user.BankId.Should().Be(bankAId);
    }

    [Fact]
    public async Task CreateAdminAsync_BankAdmin_AnalystOlusturabilir()
    {
        await sut.CreateAdminAsync(
            new CreateAdminRequest("analist@example.com", "Gecerli123", UserRole.Analyst, null));

        var user = dbContext.Users.Single(u => u.Email == "analist@example.com");
        user.Role.Should().Be(UserRole.Analyst);
        user.BankId.Should().Be(bankAId);
    }

    [Fact]
    public async Task CreateAdminAsync_SuperAdmin_BankasizBankAdminOlusturamaz()
    {
        currentUser.IsSuperAdmin.Returns(true);
        currentUser.BankId.Returns((Guid?)null);

        var act = () => sut.CreateAdminAsync(
            new CreateAdminRequest("bankasiz@example.com", "Gecerli123", UserRole.BankAdmin, null));

        await act.Should().ThrowAsync<Finara.Domain.Common.DomainException>();
    }

    [Fact]
    public async Task CreateAdminAsync_SuperAdmin_IstenenBankayaOlusturur()
    {
        currentUser.IsSuperAdmin.Returns(true);
        currentUser.BankId.Returns((Guid?)null);

        await sut.CreateAdminAsync(
            new CreateAdminRequest("bankab@example.com", "Gecerli123", UserRole.BankAdmin, bankBId));

        dbContext.Users.Single(u => u.Email == "bankab@example.com").BankId.Should().Be(bankBId);
    }

    [Fact]
    public async Task CreateAdminAsync_ZayifSifre_Reddedilir()
    {
        var act = () => sut.CreateAdminAsync(
            new CreateAdminRequest("zayif@example.com", "12", UserRole.Analyst, null));

        await act.Should().ThrowAsync<Exception>();
        dbContext.Users.Any(u => u.Email == "zayif@example.com").Should().BeFalse();
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TestDbContext(DbContextOptions<FinaraDbContext> options) : FinaraDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entityType.GetProperties())
                    if (property.GetColumnType() == "nvarchar(max)")
                        property.SetColumnType(null);
        }
    }
}
