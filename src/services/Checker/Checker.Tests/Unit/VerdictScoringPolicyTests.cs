using Checker.Application.Abstractions;
using Checker.Application.Services;
using Checker.Domain.Enums;
using Checker.Domain.ValueObjects;
using Checker.Infrastructure.Catalog;

namespace Checker.Tests.Unit;

public sealed class VerdictScoringPolicyTests
{
    private readonly IDroneSignatureCatalog _catalog = new EmbeddedDroneSignatureCatalog();
    private readonly VerdictScoringPolicy _policy = new();

    private Application.DTOs.DroneNetworkEvaluation Evaluate(string bssid, string? ssidRaw = null)
    {
        var mac = MacAddress.Parse(bssid, null);
        SsidName? ssid = null;
        if (ssidRaw is not null)
        {
            SsidName.TryCreate(ssidRaw, out ssid, out _);
        }

        var context = new EvidenceEvaluationContext(mac, ssid);
        var accumulator = new EvidenceAccumulator();
        new MacPrefixEvidenceEvaluator(_catalog).Evaluate(context, accumulator);
        new SsidEvidenceEvaluator(_catalog).Evaluate(context, accumulator);
        return _policy.EvaluateVerdict(mac, ssid, accumulator.ToResult());
    }

    [Fact]
    public void EvaluateVerdict_DedicatedDjiOuiWithoutSsid_ShouldConfirmViaOui()
    {
        var eval = Evaluate("60:60:1F:AA:BB:CC");

        Assert.True(eval.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, eval.Verdict);
        Assert.Equal(MatchSource.Oui, eval.MatchSource);
        Assert.Equal("DJI", eval.BrandMark);
        Assert.Equal("60:60:1F", eval.MatchedPrefix);
        Assert.Equal(ChipsetExclusivity.DedicatedDroneVendor, eval.ChipsetExclusivity);
    }

    [Fact]
    public void EvaluateVerdict_LongestPrefix28BitMaM_ShouldMatchAutel()
    {
        var eval = Evaluate("EC:5B:CD:E4:11:22");

        Assert.True(eval.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, eval.Verdict);
        Assert.Equal("Autel Robotics", eval.BrandMark);
        Assert.Equal("EC:5B:CD:E", eval.MatchedPrefix);
    }

    [Fact]
    public void EvaluateVerdict_SharedChipsetWithAndWithoutSsid_ShouldRespectConservativePolicy()
    {
        var confirmed = Evaluate("20:F4:1B:11:22:33", "SYMA_FPV_8821");
        Assert.True(confirmed.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, confirmed.Verdict);
        Assert.Equal(MatchSource.OuiAndSsid, confirmed.MatchSource);

        var bugsConfirmed = Evaluate("24:0A:C4:11:22:33", "Bugs_7741");
        Assert.True(bugsConfirmed.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, bugsConfirmed.Verdict);
        Assert.Equal(MatchSource.OuiAndSsid, bugsConfirmed.MatchSource);

        var possible = Evaluate("24:0A:C4:11:22:33");
        Assert.False(possible.IsDroneStation);
        Assert.Equal(DetectionVerdict.Possible, possible.Verdict);
        Assert.Equal(MatchSource.Oui, possible.MatchSource);
        Assert.Equal(ChipsetExclusivity.SharedConsumerChipset, possible.ChipsetExclusivity);
    }

    [Fact]
    public void EvaluateVerdict_LocallyAdministeredAndUnknown_ShouldApplyLaaAndNegativeRules()
    {
        var laaWithSsid = Evaluate("62:60:1F:11:22:33", "TELLO-9F2A1B");
        Assert.True(laaWithSsid.IsLocallyAdministered);
        Assert.True(laaWithSsid.IsDroneStation);
        Assert.Equal(DetectionVerdict.Probable, laaWithSsid.Verdict);
        Assert.Equal(MatchSource.Ssid, laaWithSsid.MatchSource);

        var unknown = Evaluate("00:2A:6A:01:02:03", "HomeWiFi");
        Assert.False(unknown.IsDroneStation);
        Assert.Equal(DetectionVerdict.NotRecognized, unknown.Verdict);
        Assert.Equal(MatchSource.None, unknown.MatchSource);
        Assert.Equal(0, unknown.ConfidenceScore);
    }
}
