using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class TransactionTests
{
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    [Fact]
    public void Create_GecerliBilgilerle_IslemOlusturur()
    {
        var transaction = Domain.Entities.Transaction.Create(
            ValidCustomerId, 150.75m, TransactionCategory.Groceries, "Migros", DateTime.UtcNow.AddHours(-1));

        transaction.CustomerId.Should().Be(ValidCustomerId);
        transaction.Amount.Should().Be(150.75m);
        transaction.Category.Should().Be(TransactionCategory.Groceries);
        transaction.MerchantName.Should().Be("Migros");
        transaction.Currency.Should().Be("TRY");
        transaction.IsInternational.Should().BeFalse();
    }

    [Fact]
    public void Create_BosMusteriId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.Transaction.Create(
            Guid.Empty, 100m, TransactionCategory.Fuel, "Shell", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("customerId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_SifirVeyaNegatifTutar_ArgumentExceptionFirlatir(decimal amount)
    {
        var act = () => Domain.Entities.Transaction.Create(
            ValidCustomerId, amount, TransactionCategory.Fuel, "Shell", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Create_BosIsyeriAdi_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.Transaction.Create(
            ValidCustomerId, 100m, TransactionCategory.Fuel, "  ", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("merchantName");
    }

    [Fact]
    public void Create_GelecekTarihliIslem_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.Transaction.Create(
            ValidCustomerId, 100m, TransactionCategory.Fuel, "Shell", DateTime.UtcNow.AddDays(1));

        act.Should().Throw<ArgumentException>().WithParameterName("occurredAtUtc");
    }

    [Fact]
    public void Create_YurtdisiIslemBayragiIsaretlenebilir()
    {
        var transaction = Domain.Entities.Transaction.Create(
            ValidCustomerId, 500m, TransactionCategory.Travel, "Booking.com",
            DateTime.UtcNow, currency: "USD", isInternational: true);

        transaction.IsInternational.Should().BeTrue();
        transaction.Currency.Should().Be("USD");
    }
}
