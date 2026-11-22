using System.Diagnostics;
using Checker.Domain.ValueObjects;
using Checker.Infrastructure.Catalog;

namespace Checker.Tests.Unit;

public sealed class DroneSignatureCatalogTests
{
    private readonly EmbeddedDroneSignatureCatalog _catalog = new();

    [Theory]
    [InlineData("04:A8:5A:11:22:33", "DJI")]
    [InlineData("0C:9A:E6:11:22:33", "DJI")]
    [InlineData("34:D2:62:11:22:33", "DJI")]
    [InlineData("48:1C:B9:11:22:33", "DJI")]
    [InlineData("4C:43:F6:11:22:33", "DJI")]
    [InlineData("58:B8:58:11:22:33", "DJI")]
    [InlineData("60:60:1F:11:22:33", "DJI")]
    [InlineData("88:29:85:11:22:33", "DJI")]
    [InlineData("8C:58:23:11:22:33", "DJI")]
    [InlineData("E4:7A:2C:11:22:33", "DJI")]
    [InlineData("00:12:1C:11:22:33", "Parrot")]
    [InlineData("00:26:7E:11:22:33", "Parrot")]
    [InlineData("90:03:B7:11:22:33", "Parrot")]
    [InlineData("A0:14:3D:11:22:33", "Parrot")]
    [InlineData("38:1D:14:11:22:33", "Skydio")]
    [InlineData("EC:5B:CD:E0:11:22", "Autel Robotics")]
    [InlineData("98:AA:FC:70:11:22", "Hubsan")]
    [InlineData("E0:B6:F5:80:11:22", "Yuneec")]
    public void Catalog_ShouldResolveAllVerifiedDedicatedDronePrefixes(string bssid, string expectedBrand)
    {
        Assert.True(MacAddress.TryParse(bssid, null, out var mac));
        var entry = _catalog.FindBestPrefixMatch(mac!.CompactHex, out var matched);

        Assert.NotNull(entry);
        Assert.NotNull(matched);
        Assert.Equal(expectedBrand, entry!.BrandMark);
    }

    [Theory]
    [InlineData("SYMA_FPV_1234", "Syma")]
    [InlineData("Bugs_5W_9012", "MJX")]
    [InlineData("MJX_B16_4411", "MJX")]
    [InlineData("TELLO-AB12CD", "Ryze / DJI")]
    [InlineData("DJI-Mini-9821", "DJI")]
    [InlineData("Mavic-4812", "DJI")]
    [InlineData("ANAFI-A1234", "Parrot")]
    [InlineData("Autel_EVO-1122", "Autel Robotics")]
    [InlineData("Hubsan-Zino-123", "Hubsan")]
    [InlineData("Typhoon-H-999", "Yuneec")]
    [InlineData("Skydio2-1234", "Skydio")]
    [InlineData("HolyStone-HS720-12", "Holy Stone")]
    [InlineData("HS720-4455", "Holy Stone")]
    [InlineData("Potensic-Atom-12", "Potensic")]
    [InlineData("Eachine_E58_9911", "Eachine")]
    [InlineData("JJRC_H68_1234", "JJRC")]
    [InlineData("CX-30W_1234", "Cheerson")]
    [InlineData("Walkera_Vitus_123", "Walkera")]
    [InlineData("SoloLink_9988", "3DR")]
    [InlineData("HoverAir-X1-1234", "HoverAir / Zero Zero Robotics")]
    [InlineData("ExpressLRS TX", "ExpressLRS / Open-Source FPV")]
    public void Catalog_ShouldMatchFamousDroneSsidPatterns(string ssid, string expectedBrand)
    {
        var matches = _catalog.FindMatchingSsidRules(ssid);

        Assert.NotEmpty(matches);
        Assert.Contains(matches, m => m.BrandMark == expectedBrand);
    }

    [Fact]
    public void Catalog_SsidEvaluation_ShouldBeFastAndReDoSSafe()
    {
        var hostileInputs = new[]
        {
            "Syma-" + new string('A', 27),
            "Mavic-" + new string('-', 26),
            "DJI_Mini_" + new string('9', 23),
            new string('!', 32)
        };

        var sw = Stopwatch.StartNew();
        foreach (var input in hostileInputs)
        {
            _ = _catalog.FindMatchingSsidRules(input);
        }
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(100));
    }
}
