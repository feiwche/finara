using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Finara.Infrastructure.Tests;

// AuthServiceTestBase'teki gerekçenin aynısı — SQLite in-memory, açık bağlantı testin ömrü
// boyunca canlı tutuluyor, nvarchar(max) anotasyonu SQLite ayrıştırıcısı için temizleniyor.
public abstract class CustomerServiceTestBase : IDisposable
{
    private readonly SqliteConnection connection;
    protected FinaraDbContext DbContext { get; }
    protected ICurrentUserContext CurrentUser { get; }
    protected IBankDataProviderFactory BankDataProviderFactory { get; }
    protected IAuditLogRepository AuditLogRepository { get; }
    protected CustomerService Sut { get; }
    protected Guid BankId { get; }
    protected Guid OtherBankId { get; }

    protected CustomerServiceTestBase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FinaraDbContext>().UseSqlite(connection).Options;
        DbContext = new TestFinaraDbContext(options);
        DbContext.Database.EnsureCreated();

        var bank = Bank.Create("Test Bankası", "TST");
        var otherBank = Bank.Create("Diğer Banka", "OTH");
        DbContext.Banks.AddRange(bank, otherBank);
        DbContext.SaveChanges();
        BankId = bank.Id;
        OtherBankId = otherBank.Id;

        CurrentUser = Substitute.For<ICurrentUserContext>();
        CurrentUser.IsSuperAdmin.Returns(false);
        CurrentUser.BankId.Returns(BankId);
        CurrentUser.ResolveBankIdForWrite().Returns(BankId);

        BankDataProviderFactory = Substitute.For<IBankDataProviderFactory>();
        AuditLogRepository = Substitute.For<IAuditLogRepository>();

        Sut = new CustomerService(DbContext, BankDataProviderFactory, AuditLogRepository, CurrentUser);
    }

    protected Customer CreateCustomer(
        string firstName = "Ayşe",
        string lastName = "Yılmaz",
        string email = "ayse@example.com",
        Guid? bankId = null,
        DateTime? dateOfBirth = null)
    {
        var customer = Customer.Create(bankId ?? BankId, firstName, lastName, email, dateOfBirth ?? new DateTime(1990, 1, 1));
        DbContext.Customers.Add(customer);
        DbContext.SaveChanges();
        return customer;
    }

    public void Dispose()
    {
        DbContext.Dispose();
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TestFinaraDbContext(DbContextOptions<FinaraDbContext> options) : FinaraDbContext(options)
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
