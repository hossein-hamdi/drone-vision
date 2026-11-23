using Checker.Application.DTOs;

namespace Checker.Application.Abstractions;

public interface IDroneNetworkCheckerService
{
    Task<DroneNetworkCheckResponse> CheckAsync(DroneNetworkCheckQuery query, CancellationToken ct = default);
    Task<WlanInterfacesResponse> GetWlanInterfacesAsync(CancellationToken ct = default);
    Task<ScannedDroneNetworksResponse> ScanDroneNetworksAsync(bool includePossible = false, CancellationToken ct = default);
}
