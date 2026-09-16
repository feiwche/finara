using AwesomeAssertions;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Finara.Infrastructure.Tests;

// ConsentService'in HİÇ testi yoktu (denetimde bulundu). Rıza, müşteriye ileti göndermenin
// yasal kapısı (IConsentGate tüm dağıtıcılarda zorunlu geçiş noktası) — buradaki bir izolasyon
// ya da mantık hatası doğrudan "rızası olmayan kişiye ticari ileti" demek. Ayrıca çok-kiracılı
// kontrol elle uygulanıyor, yani sessizce kaybolabilecek türden.
public class ConsentServiceTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly FinaraDbContext dbContext;
    private readonly ICurrentUserContext currentUser;
    private readonly ConsentService sut;
    private readonly Guid bankAId;
    private readonly Guid bankBId;

    public ConsentServiceTests()
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

        // Banka A'nın BankAdmin'i — Banka B'nin müşterisine erişememeli.
        currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.IsSuperAdmin.Returns(false);
        currentUser.BankId.Returns(bankAId);

        sut = new ConsentService(dbContext, Substitute.For<IAuditLogRepository>(), currentUser);
    }

    [Fact]
    public async Task GetConsentsAsync_BaskaBankaninMusterisi_NullDoner()
    {
        var foreignCustomer = CreateCustomer(bankBId, "yabanci@example.com");

        var result = await sut.GetConsentsAsync(foreignCustomer.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateConsentAsync_BaskaBankaninMusterisi_NullDonerVeKayitOlusturmaz()
    {
        var foreignCustomer = CreateCustomer(bankBId, "yabanci2@example.com");

        var result = await sut.UpdateConsentAsync(
            foreignCustomer.Id, new UpdateConsentRequest(ConsentChannel.Sms, IsGranted: true));

        result.Should().BeNull();
        // Sessizce bir rıza kaydı oluşmamalı — aksi halde başka bankanın müşterisine
        // ileti göndermenin önü açılırdı.
        dbContext.CustomerConsents.Any(c => c.CustomerId == foreignCustomer.Id).Should().BeFalse();
    }

    [Fact]
    public async Task GetConsentsAsync_KaydiOlmayanKanallariRizasiz_Doner()
    {
        var customer = CreateCustomer(bankAId, "kayitsiz@example.com");

        var result = await sut.GetConsentsAsync(customer.Id);

        // Rıza sorulmamış = rıza yok. Tüm kanallar listelenmeli ki ekranda eksik kanal olmasın.
        result.Should().NotBeNull();
        result!.Should().HaveCount(Enum.GetValues<ConsentChannel>().Length);
        result.Should().OnlyContain(c => !c.IsGranted);
    }

    [Fact]
    public async Task UpdateConsentAsync_RizaVerilipGeriAlinabilir()
    {
        var customer = CreateCustomer(bankAId, "rizali@example.com");

        var granted = await sut.UpdateConsentAsync(
            customer.Id, new UpdateConsentRequest(ConsentChannel.Email, IsGranted: true));
        granted!.IsGranted.Should().BeTrue();
        granted.GrantedAtUtc.Should().NotBeNull();

        var revoked = await sut.UpdateConsentAsync(
            customer.Id, new UpdateConsentRequest(ConsentChannel.Email, IsGranted: false));
        revoked!.IsGranted.Should().BeFalse();
        revoked.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateConsentAsync_SuperAdmin_HerBankayaErisebilir()
    {
        var foreignCustomer = CreateCustomer(bankBId, "superadmin-erisim@example.com");
        currentUser.IsSuperAdmin.Returns(true);
        currentUser.BankId.Returns((Guid?)null);

        var result = await sut.UpdateConsentAsync(
            foreignCustomer.Id, new UpdateConsentRequest(ConsentChannel.Sms, IsGranted: true));

        result.Should().NotBeNull();
        result!.IsGranted.Should().BeTrue();
    }

    private Customer CreateCustomer(Guid bankId, string email)
    {
        var customer = Customer.Create(bankId, "Test", "Müşteri", email, new DateTime(1990, 1, 1));
        dbContext.Customers.Add(customer);
        dbContext.SaveChanges();
        return customer;
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
