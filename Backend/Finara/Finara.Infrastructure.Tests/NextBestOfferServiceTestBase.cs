using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Finara.Infrastructure.Tests;

// CustomerServiceTestBase'teki aynı desen (SQLite in-memory).
public abstract class NextBestOfferServiceTestBase : IDisposable
{
    private readonly SqliteConnection connection;
    protected FinaraDbContext DbContext { get; }
    protected ICurrentUserContext CurrentUser { get; }
    protected IAuditLogRepository AuditLogRepository { get; }
    protected NextBestOfferService Sut { get; }
    protected Guid BankId { get; }

    protected NextBestOfferServiceTestBase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FinaraDbContext>().UseSqlite(connection).Options;
        DbContext = new TestFinaraDbContext(options);
        DbContext.Database.EnsureCreated();

        var bank = Bank.Create("Test Bankası", "TST");
        DbContext.Banks.Add(bank);
        DbContext.SaveChanges();
        BankId = bank.Id;

        CurrentUser = Substitute.For<ICurrentUserContext>();
        CurrentUser.IsSuperAdmin.Returns(false);
        CurrentUser.BankId.Returns(BankId);

        AuditLogRepository = Substitute.For<IAuditLogRepository>();

        Sut = new NextBestOfferService(DbContext, AuditLogRepository, CurrentUser);
    }

    protected Customer CreateCustomerWithSegment(string email, TransactionCategory dominantCategory, SpendingPropensity propensity, int recency, int frequency, int monetary)
    {
        var customer = Customer.Create(BankId, "Test", "Müşteri", email, new DateTime(1990, 1, 1));
        DbContext.Customers.Add(customer);
        DbContext.Segments.Add(Segment.Create(BankId, customer.Id, recency, frequency, monetary, propensity, dominantCategory));
        DbContext.SaveChanges();
        return customer;
    }

    protected Campaign CreateActiveCampaign(string name, TransactionCategory targetCategory)
    {
        var campaign = Campaign.Create(BankId, name, "Açıklama", DateTime.UtcNow, targetCategory: targetCategory);
        campaign.Activate();
        DbContext.Campaigns.Add(campaign);
        DbContext.SaveChanges();
        return campaign;
    }

    // Bir kampanyanın geçmiş yanıt istatistiklerini simüle etmek için — CampaignOffer normalde
    // Create()'te Pending başlıyor, burada doğrudan Accept()/Decline() ile yanıtlanmış geçmiş
    // teklifler ekleniyor (gerçek bir müşteriye bağlı olmaları gerekiyor ama HANGİ müşteriye
    // bağlı olduğu ret-geçmişi cezası için önemsiz — kampanya bazlı bir istatistik).
    protected void SeedPastResponses(Campaign campaign, int acceptedCount, int declinedCount)
    {
        for (var i = 0; i < acceptedCount + declinedCount; i++)
        {
            var pastCustomer = Customer.Create(BankId, "Geçmiş", "Müşteri", $"past-{campaign.Id}-{i}@example.com", new DateTime(1990, 1, 1));
            DbContext.Customers.Add(pastCustomer);
            var offer = CampaignOffer.Create(BankId, campaign.Id, pastCustomer.Id, confidenceScore: 50);
            if (i < acceptedCount) offer.Accept(); else offer.Decline();
            DbContext.CampaignOffers.Add(offer);
        }
        DbContext.SaveChanges();
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
