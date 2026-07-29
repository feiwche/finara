using AwesomeAssertions;
using Finara.Infrastructure.Automation;
using Xunit;

namespace Finara.Infrastructure.Tests;

public class AutomationHealthTrackerTests
{
    [Fact]
    public void GetLastSuccessUtc_HicKaydedilmemisIsIcin_NullDoner()
    {
        var tracker = new AutomationHealthTracker();

        tracker.GetLastSuccessUtc("segmentation").Should().BeNull();
    }

    [Fact]
    public void RecordSuccess_SonrakiOkumadaYakinBirZamanDamgasiDoner()
    {
        var tracker = new AutomationHealthTracker();
        var before = DateTime.UtcNow;

        tracker.RecordSuccess("segmentation");

        var recorded = tracker.GetLastSuccessUtc("segmentation");
        recorded.Should().NotBeNull();
        recorded!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void RecordSuccess_FarkliIsAdlariBirbirinIEtkilemez()
    {
        var tracker = new AutomationHealthTracker();

        tracker.RecordSuccess("segmentation");

        tracker.GetLastSuccessUtc("weeklyDigest").Should().BeNull();
    }
}
