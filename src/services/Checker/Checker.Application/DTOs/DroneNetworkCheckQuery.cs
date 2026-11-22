namespace Checker.Application.DTOs;

public sealed record DroneNetworkCheckQuery(
    string? Bssid,
    string? Ssid,
    bool SsidWasProvided = false);
