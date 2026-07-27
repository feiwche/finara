using System.Text;
using AwesomeAssertions;
using Finara.Application.Contracts;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Finara.Infrastructure.Tests;

public class CustomerServiceTests : CustomerServiceTestBase
{
    [Fact]
    public async Task CreateCustomerAsync_WithNewEmail_CreatesCustomer()
    {
        var request = new CreateCustomerRequest("Ayşe", "Yılmaz", "ayse@example.com", new DateTime(1990, 1, 1));

        var result = await Sut.CreateCustomerAsync(request);

        result.FirstName.Should().Be("Ayşe");
        result.Email.Should().Be("ayse@example.com");
        DbContext.Customers.Should().ContainSingle(c => c.Email == "ayse@example.com" && c.BankId == BankId);
    }

    [Fact]
    public async Task CreateCustomerAsync_WithDuplicateEmailInSameBank_Throws()
    {
        CreateCustomer(email: "ayse@example.com");
        var request = new CreateCustomerRequest("Ayşe", "Yılmaz", "AYSE@example.com", new DateTime(1990, 1, 1));

        var act = async () => await Sut.CreateCustomerAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateCustomerAsync_WithSameEmailInDifferentBank_Succeeds()
    {
        // Banka bazlı benzersizlik: aynı gerçek kişi iki farklı bankanın müşterisi olabilir.
        CreateCustomer(email: "ayse@example.com", bankId: OtherBankId);
        var request = new CreateCustomerRequest("Ayşe", "Yılmaz", "ayse@example.com", new DateTime(1990, 1, 1));

        var result = await Sut.CreateCustomerAsync(request);

        result.Email.Should().Be("ayse@example.com");
    }

    [Fact]
    public async Task GetCustomersAsync_AsBankAdmin_OnlyReturnsOwnBankCustomers()
    {
        CreateCustomer(email: "own@example.com");
        CreateCustomer(email: "other@example.com", bankId: OtherBankId);

        var result = await Sut.GetCustomersAsync();

        result.Should().ContainSingle(c => c.Email == "own@example.com");
    }

    [Fact]
    public async Task GetCustomersAsync_AsSuperAdmin_ReturnsAllBanks()
    {
        CurrentUser.IsSuperAdmin.Returns(true);
        CreateCustomer(email: "own@example.com");
        CreateCustomer(email: "other@example.com", bankId: OtherBankId);

        var result = await Sut.GetCustomersAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCustomersPagedAsync_ClampsOutOfRangePageSize()
    {
        for (var i = 0; i < 5; i++)
            CreateCustomer(email: $"c{i}@example.com", lastName: $"Soyad{i}");

        var result = await Sut.GetCustomersPagedAsync(page: 1, pageSize: 10_000, search: null);

        result.PageSize.Should().Be(100);
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetCustomersPagedAsync_FiltersBySearchTerm()
    {
        CreateCustomer(firstName: "Ayşe", lastName: "Yılmaz", email: "ayse@example.com");
        CreateCustomer(firstName: "Mehmet", lastName: "Demir", email: "mehmet@example.com");

        var result = await Sut.GetCustomersPagedAsync(page: 1, pageSize: 20, search: "mehmet");

        result.Items.Should().ContainSingle(c => c.FirstName == "Mehmet");
    }

    [Fact]
    public async Task GetCustomerStatsAsync_ExcludesErasedCustomers()
    {
        var erased = CreateCustomer(email: "erased@example.com");
        CreateCustomer(email: "active@example.com");
        erased.Erase();
        await DbContext.SaveChangesAsync();

        var stats = await Sut.GetCustomerStatsAsync();

        stats.TotalCustomers.Should().Be(1);
    }

    [Fact]
    public async Task RecordTransactionAsync_ForCustomerInAnotherBank_ReturnsNull()
    {
        var otherBankCustomer = CreateCustomer(email: "other@example.com", bankId: OtherBankId);
        var request = new RecordTransactionRequest(100m, TransactionCategory.Groceries, "Migros", DateTime.UtcNow);

        var result = await Sut.RecordTransactionAsync(otherBankCustomer.Id, request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RecordTransactionAsync_ForErasedCustomer_Throws()
    {
        var customer = CreateCustomer();
        customer.Erase();
        await DbContext.SaveChangesAsync();
        var request = new RecordTransactionRequest(100m, TransactionCategory.Groceries, "Migros", DateTime.UtcNow);

        var act = async () => await Sut.RecordTransactionAsync(customer.Id, request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RecordTransactionAsync_WithValidRequest_CreatesTransaction()
    {
        var customer = CreateCustomer();
        var request = new RecordTransactionRequest(250.50m, TransactionCategory.Groceries, "Migros", DateTime.UtcNow);

        var result = await Sut.RecordTransactionAsync(customer.Id, request);

        result.Should().NotBeNull();
        result!.Amount.Should().Be(250.50m);
        DbContext.Transactions.Should().ContainSingle(t => t.CustomerId == customer.Id);
    }

    [Fact]
    public async Task EraseCustomerAsync_RevokesActiveConsentsAndAnonymizes()
    {
        var customer = CreateCustomer();
        DbContext.CustomerConsents.Add(CustomerConsent.Create(BankId, customer.Id, ConsentChannel.Sms, isGranted: true));
        await DbContext.SaveChangesAsync();

        var result = await Sut.EraseCustomerAsync(customer.Id);

        result.Should().NotBeNull();
        result!.IsErased.Should().BeTrue();
        DbContext.CustomerConsents.Single(c => c.CustomerId == customer.Id).IsGranted.Should().BeFalse();
    }

    [Fact]
    public async Task ImportCustomersAsync_WithMissingRequiredColumn_Throws()
    {
        var csv = "Ad,Soyad,E-posta\nAyşe,Yılmaz,ayse@example.com\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var act = async () => await Sut.ImportCustomersAsync(stream);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ImportCustomersAsync_WithDuplicateEmailWithinFile_RecordsRowError()
    {
        var csv = "Ad,Soyad,E-posta,Doğum Tarihi\n" +
                   "Ayşe,Yılmaz,ayse@example.com,1990-01-01\n" +
                   "Ayşe2,Yılmaz2,ayse@example.com,1991-01-01\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await Sut.ImportCustomersAsync(stream);

        result.SuccessCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Row == 3);
    }

    [Fact]
    public async Task ImportCustomersAsync_WithInvalidDateFormat_RecordsRowError()
    {
        var csv = "Ad,Soyad,E-posta,Doğum Tarihi\nAyşe,Yılmaz,ayse@example.com,01/05/1990\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await Sut.ImportCustomersAsync(stream);

        result.SuccessCount.Should().Be(0);
        result.Errors.Should().ContainSingle(e => e.Row == 2);
    }

    [Fact]
    public async Task ImportCustomersAsync_WithValidRows_ImportsCustomers()
    {
        var csv = "Ad,Soyad,E-posta,Doğum Tarihi,Eğitim Durumu\n" +
                   "Ayşe,Yılmaz,ayse@example.com,1990-01-01,Lisans\n" +
                   "Mehmet,Demir,mehmet@example.com,1985-05-12,\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await Sut.ImportCustomersAsync(stream);

        result.SuccessCount.Should().Be(2);
        result.Errors.Should().BeEmpty();
        DbContext.Customers.Should().Contain(c => c.Email == "ayse@example.com" && c.EducationLevel == EducationLevel.Lisans);
    }

    [Fact]
    public async Task GenerateMockDataAsync_WithInvalidCounts_Throws()
    {
        var request = new GenerateMockDataRequest(CustomerCount: 0, TransactionsPerCustomer: 1);

        var act = async () => await Sut.GenerateMockDataAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
