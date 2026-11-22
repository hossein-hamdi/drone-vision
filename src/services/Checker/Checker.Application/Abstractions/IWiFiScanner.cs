using Checker.Application.DTOs;

namespace Checker.Application.Abstractions;

public interface IWiFiScanner
{
    Task<IReadOnlyList<ScannedWiFiNetwork>> ScanAsync(CancellationToken ct = default);
}
