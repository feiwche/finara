using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Finara.Infrastructure.Tests;

public abstract class CampaignServiceTestBase : IDisposable
{
    private readonly SqliteConnection connection;
    protected FinaraDbContext DbContext { get; }
    protected ICurrentUserContext CurrentUser { get; }
    protected IRealtimeNotifier RealtimeNotifier { get; }
    protected CampaignService Sut { get; }
    protected Guid BankId { get; }
    protected Guid OtherBankId { get; }

    protected CampaignServiceTestBase()
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

        RealtimeNotifier = Substitute.For<IRealtimeNotifier>();

        Sut = new CampaignService(DbContext, CurrentUser, RealtimeNotifier);
    }

    protected Campaign CreateCampaign(
        string name = "Yaz Kampanyası",
        Guid? bankId = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null)
    {
        var campaign = Campaign.Create(
            bankId ?? BankId,
            name,
            "Açıklama",
            startDateUtc ?? DateTime.UtcNow.AddDays(-1),
            "default",
            targetCategory: null,
            targetPropensities: null,
            endDateUtc: endDateUtc);
        DbContext.Campaigns.Add(campaign);
        DbContext.SaveChanges();
        return campaign;
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
