using AwesomeAssertions;
using Xunit;

namespace Finara.Domain.Tests;

public class AiInteractionLogTests
{
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    [Fact]
    public void Create_GecerliBilgilerle_KayitOlusturur()
    {
        var log = Domain.Entities.AiInteractionLog.Create(ValidCustomerId, "Bu müşteriye ne önerilir?", "Motosiklet Kaskosu önerilir.");

        log.CustomerId.Should().Be(ValidCustomerId);
        log.Prompt.Should().Be("Bu müşteriye ne önerilir?");
        log.Response.Should().Be("Motosiklet Kaskosu önerilir.");
    }

    [Fact]
    public void Create_BosMusteriId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.AiInteractionLog.Create(Guid.Empty, "soru", "cevap");

        act.Should().Throw<ArgumentException>().WithParameterName("customerId");
    }

    [Fact]
    public void Create_BosPrompt_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.AiInteractionLog.Create(ValidCustomerId, "", "cevap");

        act.Should().Throw<ArgumentException>().WithParameterName("prompt");
    }

    [Fact]
    public void Create_BosResponse_KabulEdilir()
    {
        var log = Domain.Entities.AiInteractionLog.Create(ValidCustomerId, "soru", "");

        log.Response.Should().Be("");
    }
}
