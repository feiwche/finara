using AwesomeAssertions;
using Finara.Application.Contracts;
using Finara.Domain.Common;
using Finara.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Finara.Infrastructure.Tests;

public class CampaignServiceTests : CampaignServiceTestBase
{
    [Fact]
    public async Task CreateCampaignAsync_WithValidRequest_CreatesDraftCampaign()
    {
        var request = new CreateCampaignRequest("Yaz Kampanyası", "Açıklama", DateTime.UtcNow.AddDays(1));

        var result = await Sut.CreateCampaignAsync(request);

        result.Name.Should().Be("Yaz Kampanyası");
        result.Status.Should().Be(CampaignStatus.Draft);
        DbContext.Campaigns.Should().ContainSingle(c => c.Name == "Yaz Kampanyası" && c.BankId == BankId);
    }

    [Fact]
    public async Task ActivateCampaignAsync_FromDraft_TransitionsToActiveAndNotifies()
    {
        var campaign = CreateCampaign();

        var result = await Sut.ActivateCampaignAsync(campaign.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(CampaignStatus.Active);
        await RealtimeNotifier.Received(1).NotifyCampaignUpdatedAsync(BankId, Arg.Any<CampaignSummary>());
    }

    [Fact]
    public async Task ActivateCampaignAsync_WithExpiredEndDate_Throws()
    {
        var campaign = CreateCampaign(endDateUtc: DateTime.UtcNow.AddDays(-1), startDateUtc: DateTime.UtcNow.AddDays(-5));

        var act = async () => await Sut.ActivateCampaignAsync(campaign.Id);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CompleteCampaignAsync_FromDraft_Throws()
    {
        // Bilinen regresyon: Complete() önceden Draft'tan bile çağrılabiliyordu, artık sadece Active'ten.
        var campaign = CreateCampaign();

        var act = async () => await Sut.CompleteCampaignAsync(campaign.Id);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CompleteCampaignAsync_FromActive_Succeeds()
    {
        var campaign = CreateCampaign();
        await Sut.ActivateCampaignAsync(campaign.Id);

        var result = await Sut.CompleteCampaignAsync(campaign.Id);

        result!.Status.Should().Be(CampaignStatus.Completed);
    }

    [Fact]
    public async Task PauseCampaignAsync_FromDraft_Throws()
    {
        var campaign = CreateCampaign();

        var act = async () => await Sut.PauseCampaignAsync(campaign.Id);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ArchiveCampaignAsync_FromAnyStatus_Succeeds()
    {
        var campaign = CreateCampaign();

        var result = await Sut.ArchiveCampaignAsync(campaign.Id);

        result!.Status.Should().Be(CampaignStatus.Archived);
    }

    [Fact]
    public async Task ActivateCampaignAsync_ForCampaignInAnotherBank_ReturnsNull()
    {
        var campaign = CreateCampaign(bankId: OtherBankId);

        var result = await Sut.ActivateCampaignAsync(campaign.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ActivateCampaignAsync_ForNonExistentCampaign_ReturnsNull()
    {
        var result = await Sut.ActivateCampaignAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCampaignsAsync_AsBankAdmin_OnlyReturnsOwnBankCampaigns()
    {
        CreateCampaign(name: "Kendi Kampanyam");
        CreateCampaign(name: "Başkasının Kampanyası", bankId: OtherBankId);

        var result = await Sut.GetCampaignsAsync();

        result.Should().ContainSingle(c => c.Name == "Kendi Kampanyam");
    }

    [Fact]
    public async Task GetCampaignsAsync_AsSuperAdmin_ReturnsAllBanks()
    {
        CurrentUser.IsSuperAdmin.Returns(true);
        CreateCampaign(name: "Kampanya A");
        CreateCampaign(name: "Kampanya B", bankId: OtherBankId);

        var result = await Sut.GetCampaignsAsync();

        result.Should().HaveCount(2);
    }
}
