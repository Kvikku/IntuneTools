using IntuneTools.Utilities;
using Xunit;

namespace IntuneTools.Tests;

public class VersionCheckTests
{
    [Theory]
    [InlineData("v1.4.0.0", "1.3.0.0", true)]
    [InlineData("1.4.0.0", "1.3.0.0", true)]
    [InlineData("v2.0.0.0", "1.9.9.9", true)]
    [InlineData("1.3.1.0", "1.3.0.0", true)]
    [InlineData("1.3.0.1", "1.3.0.0", true)]
    public void IsLatestNewer_WhenLatestIsHigher_ReturnsTrue(string latest, string current, bool expected)
    {
        Assert.Equal(expected, VersionCheck.IsLatestNewer(latest, current));
    }

    [Theory]
    [InlineData("1.3.0.0", "1.3.0.0", false)]
    [InlineData("v1.3.0.0", "1.3.0.0", false)]
    [InlineData("1.3.0.0", "v1.3.0.0", false)]
    public void IsLatestNewer_WhenVersionsAreEqual_ReturnsFalse(string latest, string current, bool expected)
    {
        Assert.Equal(expected, VersionCheck.IsLatestNewer(latest, current));
    }

    [Theory]
    [InlineData("1.2.0.0", "1.3.0.0", false)]
    [InlineData("1.0.0.0", "2.0.0.0", false)]
    public void IsLatestNewer_WhenCurrentIsHigher_ReturnsFalse(string latest, string current, bool expected)
    {
        Assert.Equal(expected, VersionCheck.IsLatestNewer(latest, current));
    }

    [Theory]
    [InlineData("", "1.3.0.0")]
    [InlineData(null, "1.3.0.0")]
    [InlineData("   ", "1.3.0.0")]
    [InlineData("1.3.0.0", "")]
    [InlineData("1.3.0.0", null)]
    [InlineData("not-a-version", "1.3.0.0")]
    [InlineData("1.3.0.0", "garbage")]
    public void IsLatestNewer_WithUnparseableInput_ReturnsFalse(string? latest, string? current)
    {
        Assert.False(VersionCheck.IsLatestNewer(latest!, current!));
    }

    [Theory]
    [InlineData("V1.4.0.0", "1.3.0.0", true)]
    [InlineData("v1.4.0.0", "V1.3.0.0", true)]
    public void IsLatestNewer_VPrefixIsCaseInsensitive(string latest, string current, bool expected)
    {
        Assert.Equal(expected, VersionCheck.IsLatestNewer(latest, current));
    }

    [Theory]
    [InlineData("1.3.0", "1.2.0", true)]
    [InlineData("1.3", "1.2", true)]
    public void IsLatestNewer_HandlesShortVersionStrings(string latest, string current, bool expected)
    {
        Assert.Equal(expected, VersionCheck.IsLatestNewer(latest, current));
    }
}
