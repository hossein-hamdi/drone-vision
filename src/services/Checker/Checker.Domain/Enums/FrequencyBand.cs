namespace Checker.Domain.Enums;

public enum FrequencyBand
{
    Band433MHz = 0,
    Band900MHz = 1,
    Band24GHz = 2,
    Band5GHz = 3,
    Band58GHz = 4
}

public static class FrequencyBandExtensions
{
    public static string ToDisplayLabel(this FrequencyBand band) => band switch
    {
        FrequencyBand.Band433MHz => "433 MHz",
        FrequencyBand.Band900MHz => "900 MHz",
        FrequencyBand.Band24GHz => "2.4 GHz",
        FrequencyBand.Band5GHz => "5 GHz",
        FrequencyBand.Band58GHz => "5.8 GHz",
        _ => band.ToString()
    };
}
