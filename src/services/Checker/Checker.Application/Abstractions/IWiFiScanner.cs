using Checker.Application.DTOs;

namespace Checker.Application.Abstractions;

public interface IWiFiScanner
{
    Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default);
    Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default);
}
