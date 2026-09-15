using AwesomeAssertions;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Automation;
using Finara.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Finara.Infrastructure.Tests;

// Bu kuralın bozulması doğrudan gerçek müşterilere ileti spam'i demek — ilk sürümde tam olarak
// bu oldu (bir müşteriye 1 saniye içinde 10 ileti gitti, canlı veride görüldü). Testler o
// davranışı kalıcı olarak kilitliyor.
public class OfferDistributionPlannerTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly FinaraDbContext dbContext;
    private readonly Guid bankId;

    public OfferDistributionPlannerTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FinaraDbContext>().UseSqlite(connection).Options;
        dbContext = new TestDbContext(options);
        dbContext.Database.EnsureCreated();

        var bank = Bank.Create("Test Bankası", "TST");
        dbContext.Banks.Add(bank);
        dbContext.SaveChanges();
        bankId = bank.Id;
    }

    [Fact]
    public async Task Ayni_musteriye_tur_basina_sadece_en_yuksek_skorlu_tek_teklif_secilir()
    {
        var customer = CreateCustomer("tek@example.com");
        CreateCampaignOffer(customer, confidenceScore: 40);
        var best = CreateCampaignOffer(customer, confidenceScore: 91);
        CreateCampaignOffer(customer, confidenceScore: 77);
        await dbContext.SaveChangesAsync();

        var selected = await OfferDistributionPlanner.SelectAsync(dbContext, new AutomationOptions());

        selected.Should().ContainSingle();
        selected[0].Id.Should().Be(best.Id);
    }

    [Fact]
    public async Task Yakin_zamanda_ileti_almis_musteriye_yeni_teklif_secilmez()
    {
        var customer = CreateCustomer("sessiz@example.com");
        var offer = CreateCampaignOffer(customer, confidenceScore: 95);
        // Bu teklif hiç dağıtılmadı ama müşteri 1 saat önce BAŞKA bir ileti aldı.
        dbContext.NotificationLogs.Add(NotificationLog.Create(
            bankId, customer.Id, ConsentChannel.Sms, "önceki ileti", sent: true, skippedReason: null, campaignOfferId: null));
        await dbContext.SaveChangesAsync();

        var selected = await OfferDistributionPlanner.SelectAsync(dbContext, new AutomationOptions());

        selected.Should().BeEmpty();
        offer.Response.Should().Be(OfferResponse.Pending);
    }

    [Fact]
    public async Task Bekleme_suresi_sifirlanirsa_musteri_tekrar_secilebilir()
    {
        var customer = CreateCustomer("beklemesiz@example.com");
        CreateCampaignOffer(customer, confidenceScore: 95);
        dbContext.NotificationLogs.Add(NotificationLog.Create(
            bankId, customer.Id, ConsentChannel.Sms, "önceki ileti", sent: true, skippedReason: null, campaignOfferId: null));
        await dbContext.SaveChangesAsync();

        var selected = await OfferDistributionPlanner.SelectAsync(
            dbContext, new AutomationOptions { MinHoursBetweenCustomerMessages = 0 });

        selected.Should().ContainSingle();
    }

    [Fact]
    public async Task Zaten_dagitilmis_teklif_tekrar_secilmez()
    {
        var customer = CreateCustomer("dagitilmis@example.com");
        var offer = CreateCampaignOffer(customer, confidenceScore: 88);
        await dbContext.SaveChangesAsync();
        dbContext.NotificationLogs.Add(NotificationLog.Create(
            bankId, customer.Id, ConsentChannel.Sms, "gönderildi", sent: true, skippedReason: null, campaignOfferId: offer.Id));
        await dbContext.SaveChangesAsync();

        var selected = await OfferDistributionPlanner.SelectAsync(
            dbContext, new AutomationOptions { MinHoursBetweenCustomerMessages = 0 });

        selected.Should().BeEmpty();
    }

    [Fact]
    public async Task Tur_basina_ust_sinir_musteri_sayisina_uygulanir()
    {
        for (var i = 0; i < 5; i++)
        {
            var customer = CreateCustomer($"musteri{i}@example.com");
            CreateCampaignOffer(customer, confidenceScore: 50 + i);
        }
        await dbContext.SaveChangesAsync();

        var selected = await OfferDistributionPlanner.SelectAsync(
            dbContext, new AutomationOptions { MaxDistributionsPerCycle = 2 });

        selected.Should().HaveCount(2);
        // En yüksek skorlular önce seçilir.
        selected.Select(o => o.ConfidenceScore).Should().BeInDescendingOrder();
        selected[0].ConfidenceScore.Should().Be(54);
    }

    [Fact]
    public async Task Yanitlanmis_teklifler_hic_secilmez()
    {
        var customer = CreateCustomer("yanitlanmis@example.com");
        var offer = CreateCampaignOffer(customer, confidenceScore: 99);
        offer.Accept();
        await dbContext.SaveChangesAsync();

        var selected = await OfferDistributionPlanner.SelectAsync(
            dbContext, new AutomationOptions { MinHoursBetweenCustomerMessages = 0 });

        selected.Should().BeEmpty();
    }

    private Customer CreateCustomer(string email)
    {
        var customer = Customer.Create(bankId, "Test", "Müşteri", email, new DateTime(1990, 1, 1));
        dbContext.Customers.Add(customer);
        return customer;
    }

    private CampaignOffer CreateCampaignOffer(Customer customer, double confidenceScore)
    {
        var campaign = Campaign.Create(bankId, $"Kampanya {Guid.NewGuid():N}", "Açıklama", DateTime.UtcNow);
        dbContext.Campaigns.Add(campaign);
        var offer = CampaignOffer.Create(bankId, campaign.Id, customer.Id, confidenceScore);
        dbContext.CampaignOffers.Add(offer);
        return offer;
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // CustomerServiceTestBase'teki aynı desen — SQLite'ta karşılığı olmayan nvarchar(max)
    // kolon tipi temizleniyor.
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
