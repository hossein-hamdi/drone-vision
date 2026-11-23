using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Checker.Application.Abstractions;
using Checker.Application.Common;
using Checker.Application.DTOs;
using Checker.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Checker.Infrastructure.Scanning;

public sealed partial class PlatformWiFiScanner(ILogger<PlatformWiFiScanner> logger) : IWiFiScanner
{
    public const string UnavailableReason =
        "Live wireless scanning is unavailable because no accessible WLAN interfaces were detected in this environment.";

    private static readonly TimeSpan DiscoveryCommandTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan PerInterfaceScanTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan TotalScanBudget = TimeSpan.FromSeconds(12);

    private static readonly string[] DBusSocketPaths =
    [
        "/run/dbus/system_bus_socket",
        "/var/run/dbus/system_bus_socket"
    ];

    [GeneratedRegex(@"^[A-Za-z0-9._\- ]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeInterfaceNameRegex();

    public static bool IsValidInterfaceName(string? interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return false;
        }

        var trimmed = interfaceName.Trim();
        return !trimmed.StartsWith('-') &&
               !trimmed.Any(char.IsControl) &&
               SafeInterfaceNameRegex().IsMatch(trimmed);
    }

    public Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            return DiscoverWindowsInterfacesAsync(ct);
        }

        if (OperatingSystem.IsLinux())
        {
            return DiscoverLinuxInterfacesAsync(ct);
        }

        return Task.FromResult(new WlanInterfaceDiscoveryResult(
            Available: false,
            Reason: "Live wireless scanning is only supported on Windows or Linux hosts.",
            Interfaces: []));
    }

    public async Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw ApiException.ServiceUnavailable("Live Wi-Fi scanning is only supported on Windows or Linux hosts.");
        }

        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TotalScanBudget);

        var discovery = await DiscoverInterfacesAsync(budgetCts.Token);
        if (!discovery.Available)
        {
            throw ApiException.ServiceUnavailable(discovery.Reason ?? UnavailableReason);
        }

        var usableInterfaces = discovery.Interfaces
            .Where(static i => i.IsUsableForScan && IsValidInterfaceName(i.Name))
            .Select(static i => i.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (usableInterfaces.Length == 0)
        {
            throw ApiException.ServiceUnavailable(
                "No usable wireless network interfaces are ready for scanning on this host.");
        }

        return OperatingSystem.IsWindows()
            ? await ScanWindowsAllAsync(usableInterfaces, budgetCts.Token, ct)
            : await ScanLinuxAllAsync(usableInterfaces, budgetCts.Token, ct);
    }

    private async Task<WlanInterfaceDiscoveryResult> DiscoverWindowsInterfacesAsync(CancellationToken ct)
    {
        try
        {
            var netshPath = Path.Combine(Environment.SystemDirectory, "netsh.exe");
            var (exitCode, stdout, stderr) = await RunCommandAsync(
                netshPath,
                ["wlan", "show", "interfaces"],
                DiscoveryCommandTimeout,
                ct);

            if (exitCode != 0 ||
                stdout.Contains("There is no wireless interface", StringComparison.OrdinalIgnoreCase) ||
                stdout.Contains("is not running", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Windows WLAN interface discovery unavailable. ExitCode: {ExitCode}, Stderr: {Stderr}", exitCode, stderr);
                return new WlanInterfaceDiscoveryResult(false, UnavailableReason, []);
            }

            var interfaces = NetshWlanInterfacesParser.Parse(stdout)
                .Where(static i => IsValidInterfaceName(i.Name))
                .ToArray();

            return interfaces.Length == 0
                ? new WlanInterfaceDiscoveryResult(false, UnavailableReason, [])
                : new WlanInterfaceDiscoveryResult(true, null, interfaces);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Windows WLAN interface discovery failed.");
            return new WlanInterfaceDiscoveryResult(false, UnavailableReason, []);
        }
    }

    private Task<WlanInterfaceDiscoveryResult> DiscoverLinuxInterfacesAsync(CancellationToken ct) =>
        DiscoverLinuxInterfacesForDiagnosticsAsync("/sys/class/net", HasLinuxDbusSocket(), ct);

    internal async Task<WlanInterfaceDiscoveryResult> DiscoverLinuxInterfacesForDiagnosticsAsync(
        string sysClassNetPath,
        bool hasDbus,
        CancellationToken ct)
    {
        var sysfsInterfaces = ReadLinuxSysfsWirelessInterfaces(sysClassNetPath);

        if (sysfsInterfaces.Count == 0 && !hasDbus)
        {
            // Fast path for Windows Docker (WSL2 Linux VM) or containers without wireless devices.
            return new WlanInterfaceDiscoveryResult(false, UnavailableReason, []);
        }

        var discovered = new Dictionary<string, WlanInterfaceInfo>(StringComparer.OrdinalIgnoreCase);

        if (sysfsInterfaces.Count > 0)
        {
            try
            {
                var (exitCode, stdout, stderr) = await RunCommandAsync(
                    "iw",
                    ["dev"],
                    DiscoveryCommandTimeout,
                    ct);

                if (exitCode == 0)
                {
                    var operStates = sysfsInterfaces.ToDictionary(
                        static kvp => kvp.Key,
                        static kvp => kvp.Value.State,
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var iface in IwWiFiScanParser.ParseInterfaces(stdout, operStates))
                    {
                        if (IsValidInterfaceName(iface.Name))
                        {
                            discovered[iface.Name] = iface;
                        }
                    }
                }
                else
                {
                    logger.LogDebug("Linux iw dev exited with code {ExitCode}: {Stderr}", exitCode, stderr);
                }
            }
            catch (Win32Exception ex)
            {
                logger.LogDebug(ex, "iw executable not found on Linux host.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Linux iw dev failed; falling back to sysfs/nmcli.");
            }

            foreach (var (name, sysInfo) in sysfsInterfaces)
            {
                if (!discovered.ContainsKey(name) && IsValidInterfaceName(name))
                {
                    discovered[name] = sysInfo;
                }
            }
        }

        if (hasDbus)
        {
            try
            {
                var (exitCode, stdout, _) = await RunCommandAsync(
                    "nmcli",
                    ["-t", "-f", "DEVICE,TYPE,STATE,CONNECTION", "dev", "status"],
                    DiscoveryCommandTimeout,
                    ct);

                if (exitCode == 0)
                {
                    foreach (var nmIface in NmcliWiFiScanParser.ParseInterfaces(stdout))
                    {
                        if (!IsValidInterfaceName(nmIface.Name))
                        {
                            continue;
                        }

                        if (!discovered.TryGetValue(nmIface.Name, out var existing))
                        {
                            discovered[nmIface.Name] = nmIface;
                        }
                        else if (!existing.IsUsableForScan && nmIface.IsUsableForScan)
                        {
                            discovered[nmIface.Name] = existing with { State = nmIface.State, IsUsableForScan = true };
                        }
                    }
                }
            }
            catch (Win32Exception)
            {
                // nmcli optional when iw/sysfs already discovered interfaces.
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Linux nmcli interface query failed.");
            }
        }

        if (discovered.Count == 0)
        {
            return new WlanInterfaceDiscoveryResult(false, UnavailableReason, []);
        }

        var ordered = discovered.Values
            .OrderBy(static i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WlanInterfaceDiscoveryResult(true, null, ordered);
    }

    private async Task<WiFiScanBatch> ScanWindowsAllAsync(
        IReadOnlyList<string> usableInterfaces,
        CancellationToken budgetCt,
        CancellationToken callerCt)
    {
        var netshPath = Path.Combine(Environment.SystemDirectory, "netsh.exe");
        var scannedInterfaces = new List<string>(usableInterfaces.Count);
        var observations = new List<ScannedWiFiNetwork>();

        foreach (var iface in usableInterfaces)
        {
            budgetCt.ThrowIfCancellationRequested();

            try
            {
                var (exitCode, stdout, stderr) = await RunCommandAsync(
                    netshPath,
                    ["wlan", "show", "networks", $"interface={iface}", "mode=bssid"],
                    PerInterfaceScanTimeout,
                    budgetCt);

                if (exitCode != 0 ||
                    stdout.Contains("There is no wireless interface", StringComparison.OrdinalIgnoreCase) ||
                    stdout.Contains("is not running", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Windows Wi-Fi scan failed on interface {Interface}. ExitCode: {ExitCode}, Stderr: {Stderr}", iface, exitCode, stderr);
                    continue;
                }

                scannedInterfaces.Add(iface);
                observations.AddRange(NetshWiFiScanParser.Parse(stdout, iface));
            }
            catch (OperationCanceledException) when (!callerCt.IsCancellationRequested)
            {
                logger.LogWarning("Windows Wi-Fi scan timed out on interface {Interface}.", iface);
                if (scannedInterfaces.Count > 0)
                {
                    break;
                }

                throw ApiException.ServiceUnavailable("Wireless network scan timed out.");
            }
        }

        if (scannedInterfaces.Count == 0)
        {
            throw ApiException.ServiceUnavailable("No active wireless network interface is available on this host.");
        }

        return new WiFiScanBatch(scannedInterfaces, observations);
    }

    private async Task<WiFiScanBatch> ScanLinuxAllAsync(
        IReadOnlyList<string> usableInterfaces,
        CancellationToken budgetCt,
        CancellationToken callerCt)
    {
        var hasDbus = HasLinuxDbusSocket();
        var scannedInterfaces = new List<string>(usableInterfaces.Count);
        var observations = new List<ScannedWiFiNetwork>();
        var sawMissingTool = false;

        foreach (var iface in usableInterfaces)
        {
            budgetCt.ThrowIfCancellationRequested();

            var (success, ifaceResults, missingTool) = await TryScanSingleLinuxInterfaceAsync(iface, hasDbus, budgetCt, callerCt);
            if (missingTool)
            {
                sawMissingTool = true;
            }

            if (success)
            {
                scannedInterfaces.Add(iface);
                observations.AddRange(ifaceResults);
            }
        }

        if (scannedInterfaces.Count == 0)
        {
            if (sawMissingTool)
            {
                throw ApiException.ServiceUnavailable("Wireless scanning tools are unavailable on this host.");
            }

            throw ApiException.ServiceUnavailable(
                "Unable to execute wireless scan on the available WLAN interfaces.");
        }

        return new WiFiScanBatch(scannedInterfaces, observations);
    }

    private async Task<(bool Success, IReadOnlyList<ScannedWiFiNetwork> Networks, bool MissingTool)> TryScanSingleLinuxInterfaceAsync(
        string iface,
        bool hasDbus,
        CancellationToken budgetCt,
        CancellationToken callerCt)
    {
        var missingTool = false;

        try
        {
            var (exitCode, stdout, stderr) = await RunCommandAsync(
                "iw",
                ["dev", iface, "scan"],
                PerInterfaceScanTimeout,
                budgetCt);

            if (exitCode == 0)
            {
                return (true, IwWiFiScanParser.ParseScanOutput(stdout, iface), false);
            }

            logger.LogDebug("iw dev {Interface} scan failed ({ExitCode}): {Stderr}", iface, exitCode, stderr);

            if (stderr.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Permission denied", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Resource busy", StringComparison.OrdinalIgnoreCase))
            {
                var (dumpCode, dumpOut, dumpErr) = await RunCommandAsync(
                    "iw",
                    ["dev", iface, "scan", "dump"],
                    PerInterfaceScanTimeout,
                    budgetCt);

                if (dumpCode == 0)
                {
                    return (true, IwWiFiScanParser.ParseScanOutput(dumpOut, iface), false);
                }

                logger.LogDebug("iw dev {Interface} scan dump failed ({ExitCode}): {Stderr}", iface, dumpCode, dumpErr);
            }
        }
        catch (Win32Exception ex)
        {
            missingTool = true;
            logger.LogDebug(ex, "iw executable not available on Linux host.");
        }
        catch (OperationCanceledException) when (!callerCt.IsCancellationRequested)
        {
            logger.LogWarning("Linux iw scan timed out on interface {Interface}.", iface);
        }

        if (hasDbus)
        {
            try
            {
                var (exitCode, stdout, stderr) = await RunCommandAsync(
                    "nmcli",
                    ["-t", "-f", "SSID,BSSID,SIGNAL,CHAN,FREQ,SECURITY", "dev", "wifi", "list", "ifname", iface, "--rescan", "yes"],
                    PerInterfaceScanTimeout,
                    budgetCt);

                if (exitCode == 0)
                {
                    return (true, NmcliWiFiScanParser.Parse(stdout, iface), false);
                }

                logger.LogWarning("Linux nmcli scan failed on {Interface}. ExitCode: {ExitCode}, Stderr: {Stderr}", iface, exitCode, stderr);
            }
            catch (Win32Exception ex)
            {
                missingTool = true;
                logger.LogWarning(ex, "nmcli executable not available on Linux host.");
            }
            catch (OperationCanceledException) when (!callerCt.IsCancellationRequested)
            {
                logger.LogWarning("Linux nmcli scan timed out on interface {Interface}.", iface);
            }
        }

        return (false, [], missingTool);
    }

    internal static Dictionary<string, WlanInterfaceInfo> ReadLinuxSysfsWirelessInterfaces(
        string sysClassNet = "/sys/class/net")
    {
        var results = new Dictionary<string, WlanInterfaceInfo>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(sysClassNet))
        {
            return results;
        }

        foreach (var dir in Directory.EnumerateDirectories(sysClassNet))
        {
            var ifaceName = Path.GetFileName(dir);
            if (!IsValidInterfaceName(ifaceName))
            {
                continue;
            }

            var hasWireless = Directory.Exists(Path.Combine(dir, "wireless")) ||
                              Directory.Exists(Path.Combine(dir, "phy80211"));
            if (!hasWireless)
            {
                continue;
            }

            var (state, isUsable) = ReadSysfsInterfaceState(dir);
            string? mac = null;
            var addrPath = Path.Combine(dir, "address");
            if (File.Exists(addrPath))
            {
                var rawAddr = File.ReadAllText(addrPath).Trim();
                if (MacAddress.TryParse(rawAddr, null, out var parsed))
                {
                    mac = parsed.Canonical;
                }
            }

            results[ifaceName] = new WlanInterfaceInfo(
                Name: ifaceName,
                State: state,
                IsUsableForScan: isUsable,
                Description: "802.11 Wireless Interface",
                MacAddress: mac);
        }

        return results;
    }

    private static (string State, bool IsUsable) ReadSysfsInterfaceState(string ifaceDir)
    {
        var isAdminUp = true;
        var flagsPath = Path.Combine(ifaceDir, "flags");
        if (File.Exists(flagsPath))
        {
            var rawFlags = File.ReadAllText(flagsPath).Trim();
            if (rawFlags.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(rawFlags[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags))
            {
                isAdminUp = (flags & 0x1) != 0;
            }
        }

        if (!isAdminUp)
        {
            return ("down", false);
        }

        var operPath = Path.Combine(ifaceDir, "operstate");
        var oper = File.Exists(operPath) ? File.ReadAllText(operPath).Trim().ToLowerInvariant() : "unknown";
        return oper == "up" ? ("up", true) : ("disconnected", true);
    }

    private static bool HasLinuxDbusSocket() =>
        DBusSocketPaths.Any(static path => File.Exists(path));

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCommandAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            return (process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw ApiException.ServiceUnavailable("Wireless network scan timed out.");
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup on cancellation/timeout.
        }
    }
}
