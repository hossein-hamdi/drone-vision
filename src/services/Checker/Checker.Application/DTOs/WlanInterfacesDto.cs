namespace Checker.Application.DTOs;

/// <summary>
/// Internal representation of a discovered host wireless (WLAN) network interface.
/// </summary>
public sealed record WlanInterfaceInfo(
    string Name,
    string State,
    bool IsUsableForScan,
    string? Description = null,
    string? MacAddress = null);

/// <summary>
/// Internal discovery outcome describing whether host WLAN scanning is available and listing found adapters.
/// </summary>
public sealed record WlanInterfaceDiscoveryResult(
    bool Available,
    string? Reason,
    IReadOnlyList<WlanInterfaceInfo> Interfaces);

/// <summary>
/// Public API DTO representing a single host WLAN interface.
/// </summary>
public sealed record WlanInterfaceDto
{
    public required string Name { get; init; }
    public required string State { get; init; }
    public required bool IsUsableForScan { get; init; }
    public string? Description { get; init; }
    public string? MacAddress { get; init; }
}

/// <summary>
/// Public API response payload for <c>GET /networks/interfaces</c>.
/// </summary>
public sealed record WlanInterfacesResponse
{
    public required bool Available { get; init; }
    public string? Reason { get; init; }
    public required IReadOnlyList<WlanInterfaceDto> Interfaces { get; init; }
}
