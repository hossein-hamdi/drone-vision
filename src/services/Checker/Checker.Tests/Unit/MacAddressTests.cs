using Checker.Domain.ValueObjects;

namespace Checker.Tests.Unit;

public sealed class MacAddressTests
{
    [Theory]
    [InlineData("60:60:1F:12:34:56", "60:60:1F:12:34:56", "60601F123456", "60:60:1F", "60601F1", "60601F123")]
    [InlineData("60-60-1f-12-34-56", "60:60:1F:12:34:56", "60601F123456", "60:60:1F", "60601F1", "60601F123")]
    [InlineData("6060.1F12.3456", "60:60:1F:12:34:56", "60601F123456", "60:60:1F", "60601F1", "60601F123")]
    [InlineData("  60601f123456  ", "60:60:1F:12:34:56", "60601F123456", "60:60:1F", "60601F1", "60601F123")]
    public void TryParse_ShouldNormalizeStandardMacFormats(
        string raw,
        string expectedCanonical,
        string expectedCompact,
        string expectedOui,
        string expectedPrefix28,
        string expectedPrefix36)
    {
        var ok = MacAddress.TryParse(raw, null, out var mac);

        Assert.True(ok);
        Assert.NotNull(mac);
        Assert.Equal(expectedCanonical, mac.Canonical);
        Assert.Equal(expectedCompact, mac.CompactHex);
        Assert.Equal(expectedOui, mac.Oui24);
        Assert.Equal(expectedPrefix28, mac.Prefix28);
        Assert.Equal(expectedPrefix36, mac.Prefix36);
        Assert.False(mac.IsLocallyAdministered);
        Assert.False(mac.IsMulticast);
    }

    [Theory]
    [InlineData("62:60:1F:12:34:56", true)]
    [InlineData("06:11:22:33:44:55", true)]
    [InlineData("0A:11:22:33:44:55", true)]
    [InlineData("0E:11:22:33:44:55", true)]
    [InlineData("60:60:1F:12:34:56", false)]
    public void TryParse_ShouldDetectLocallyAdministeredBit(string raw, bool expectedLaa)
    {
        Assert.True(MacAddress.TryParse(raw, null, out var mac));
        Assert.Equal(expectedLaa, mac!.IsLocallyAdministered);
    }

    [Fact]
    public void TryParse_ShouldFlagNonBroadcastMulticastBit()
    {
        Assert.True(MacAddress.TryParse("01:00:5E:00:00:01", null, out var mac));
        Assert.True(mac!.IsMulticast);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00:00:00:00:00:00")]
    [InlineData("000000000000")]
    [InlineData("FF:FF:FF:FF:FF:FF")]
    [InlineData("ff-ff-ff-ff-ff-ff")]
    [InlineData("60:60:1F:12:34")]
    [InlineData("60:60:1F:12:34:56:78")]
    [InlineData("60:60:1G:12:34:56")]
    [InlineData("60:60:1F-12:34:56")]
    [InlineData("6060.1F12.345")]
    [InlineData("not-a-mac")]
    public void TryParse_ShouldRejectInvalidZeroAndBroadcastAddresses(string? raw)
    {
        Assert.False(MacAddress.TryParse(raw, null, out var mac));
        Assert.Null(mac);
    }
}
