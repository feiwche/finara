using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Finara.Infrastructure.Tests;

// CustomerServiceTestBase'teki aynı desen (SQLite in-memory) — RecalculateAllAsync'in banka
// bazlı RFM izolasyonunu (bkz. SegmentationService.cs'teki yorum) test etmek için CurrentUser
// BİLEREK IsSuperAdmin=true veriyor, tıpkı otomasyon döngüsündeki SystemCurrentUserContext gibi
// (bu bug tam olarak SuperAdmin/otomasyon yolunda ortaya çıkıyordu).
public abstract class SegmentationServiceTestBase : IDisposable
{
    private readonly SqliteConnection connection;
    protected FinaraDbContext DbContext { get; }
    protected ICurrentUserContext CurrentUser { get; }
    protected IAuditLogRepository AuditLogRepository { get; }
    protected SegmentationService Sut { get; }
    protected Guid BankAId { get; }
    protected Guid BankBId { get; }

    protected SegmentationServiceTestBase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FinaraDbContext>().UseSqlite(connection).Options;
        DbContext = new TestFinaraDbContext(options);
        DbContext.Database.EnsureCreated();

        var bankA = Bank.Create("Banka A", "BKA");
        var bankB = Bank.Create("Banka B", "BKB");
        DbContext.Banks.AddRange(bankA, bankB);
        DbContext.SaveChanges();
        BankAId = bankA.Id;
        BankBId = bankB.Id;

        // SuperAdmin=true — otomatik segmentasyon döngüsünün gerçek kimliğiyle (SystemCurrentUserContext)
        // birebir aynı: bankId filtresi uygulanmıyor, RecalculateAllAsync'in TÜM bankaları
        // taraması gerekiyor (banka-içi izolasyonun servisin KENDİSİ tarafından sağlanması lazım).
        CurrentUser = Substitute.For<ICurrentUserContext>();
        CurrentUser.IsSuperAdmin.Returns(true);
        CurrentUser.BankId.Returns((Guid?)null);

        AuditLogRepository = Substitute.For<IAuditLogRepository>();
        var chatClient = Substitute.For<IChatClient>();

        Sut = new SegmentationService(DbContext, AuditLogRepository, CurrentUser, chatClient);
    }

    protected Customer CreateCustomer(Guid bankId, string email)
    {
        var customer = Customer.Create(bankId, "Test", "Müşteri", email, new DateTime(1990, 1, 1));
        DbContext.Customers.Add(customer);
        DbContext.SaveChanges();
        return customer;
    }

    protected void AddTransaction(Customer customer, Guid bankId, decimal amount, DateTime occurredAtUtc) =>
        DbContext.Transactions.Add(Transaction.Create(bankId, customer.Id, amount, TransactionCategory.Groceries, "Test Merchant", occurredAtUtc));

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

            // SQLite'ın EF sağlayıcısı decimal kolonlar üzerinde Sum()/Avg() gibi toplama
            // fonksiyonlarını çeviremiyor ("cannot apply aggregate operator 'Sum' on
            // expressions of type 'decimal'") — SegmentationService'in GroupBy+Sum(Amount)
            // sorgusu (RecalculateAllAsync) bu yüzden burada çöküyordu. Gerçek üretim veritabanı
            // (SQL Server) bu kısıtlamaya sahip değil — bu SADECE testteki SQLite sağlayıcısına
            // özel bir uyumluluk çözümü, üretim davranışını etkilemiyor.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.GetColumnType() == "nvarchar(max)")
                        property.SetColumnType(null);
                    if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                        property.SetProviderClrType(typeof(double));
                }
            }
        }
    }
}
