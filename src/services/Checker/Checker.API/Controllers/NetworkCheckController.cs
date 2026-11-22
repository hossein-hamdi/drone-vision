using Checker.Application.Abstractions;
using Checker.Application.Common;
using Checker.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Checker.API.Controllers;

/// <summary>
/// Provides wireless station checks against known drone OUI/SSID signatures and live Wi-Fi drone scanning.
/// </summary>
[Route("networks")]
public sealed class NetworkCheckController(IDroneNetworkCheckerService checkerService) : ApiController
{
    private static readonly HashSet<string> AllowedCheckQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "bssid",
        "ssid"
    };

    private static readonly HashSet<string> AllowedScanQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "includePossible"
    };

    /// <summary>
    /// Inspects a wireless station BSSID (and optional SSID) to determine whether it belongs to a drone station.
    /// </summary>
    /// <param name="bssid">Required station BSSID/MAC address in colon, hyphen, dot, or bare 12-hex format.</param>
    /// <param name="ssid">Optional observed Wi-Fi SSID (max 32 UTF-8 bytes) to improve model attribution and confirm shared chipsets.</param>
    /// <param name="ct">Request cancellation token.</param>
    [HttpGet("check")]
    [ProducesResponseType<DroneNetworkCheckResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DroneNetworkCheckResponse>> Check(
        [FromQuery] string? bssid,
        [FromQuery] string? ssid,
        CancellationToken ct)
    {
        RejectUnexpectedQueryKeys(AllowedCheckQueryKeys);

        var ssidWasProvided = Request.Query.ContainsKey("ssid");
        var result = await checkerService.CheckAsync(new DroneNetworkCheckQuery(bssid, ssid, ssidWasProvided), ct);
        return Ok(result);
    }

    /// <summary>
    /// Scans nearby Wi-Fi networks using the host wireless adapter and returns only the filtered drone stations.
    /// </summary>
    /// <param name="includePossible">Optional flag to include uncorroborated shared Wi-Fi chipsets (default: false).</param>
    /// <param name="ct">Request cancellation token.</param>
    [HttpGet("scan")]
    [ProducesResponseType<ScannedDroneNetworksResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ScannedDroneNetworksResponse>> Scan(
        [FromQuery] bool includePossible = false,
        CancellationToken ct = default)
    {
        RejectUnexpectedQueryKeys(AllowedScanQueryKeys);

        var result = await checkerService.ScanDroneNetworksAsync(includePossible, ct);
        return Ok(result);
    }

    private void RejectUnexpectedQueryKeys(HashSet<string> allowedKeys)
    {
        var unknownKeys = Request.Query.Keys
            .Where(key => !allowedKeys.Contains(key))
            .ToArray();

        if (unknownKeys.Length == 0)
        {
            return;
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var key in unknownKeys)
        {
            errors[key] = [$"Unexpected query parameter '{key}'."];
        }

        throw ApiException.Validation(errors);
    }
}
