using System.ComponentModel;
using System.Diagnostics;
using Checker.Application.Abstractions;
using Checker.Application.Common;
using Checker.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Checker.Infrastructure.Scanning;

public sealed class PlatformWiFiScanner(ILogger<PlatformWiFiScanner> logger) : IWiFiScanner
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(8);

    public Task<IReadOnlyList<ScannedWiFiNetwork>> ScanAsync(CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            return ScanWindowsAsync(ct);
        }

        if (OperatingSystem.IsLinux())
        {
            return ScanLinuxAsync(ct);
        }

        throw ApiException.ServiceUnavailable("Live Wi-Fi scanning is only supported on Windows or Linux hosts.");
    }

    private async Task<IReadOnlyList<ScannedWiFiNetwork>> ScanWindowsAsync(CancellationToken ct)
    {
        var netshPath = Path.Combine(Environment.SystemDirectory, "netsh.exe");
        var (exitCode, stdout, stderr) = await RunCommandAsync(
            netshPath,
            ["wlan", "show", "networks", "mode=bssid"],
            ct);

        if (exitCode != 0 ||
            stdout.Contains("There is no wireless interface", StringComparison.OrdinalIgnoreCase) ||
            stdout.Contains("is not running", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Windows Wi-Fi scan unavailable. ExitCode: {ExitCode}, Stderr: {Stderr}", exitCode, stderr);
            throw ApiException.ServiceUnavailable(
                "No active wireless network interface is available on this host.");
        }

        return NetshWiFiScanParser.Parse(stdout);
    }

    private async Task<IReadOnlyList<ScannedWiFiNetwork>> ScanLinuxAsync(CancellationToken ct)
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunCommandAsync(
                "nmcli",
                ["-t", "-f", "SSID,BSSID,SIGNAL,CHAN,FREQ,SECURITY", "dev", "wifi", "list", "--rescan", "yes"],
                ct);

            if (exitCode != 0)
            {
                logger.LogWarning("Linux nmcli scan failed. ExitCode: {ExitCode}, Stderr: {Stderr}", exitCode, stderr);
                throw ApiException.ServiceUnavailable("Unable to execute wireless scan on the Linux host.");
            }

            return NmcliWiFiScanParser.Parse(stdout);
        }
        catch (Win32Exception ex)
        {
            logger.LogWarning(ex, "nmcli executable not available on Linux host.");
            throw ApiException.ServiceUnavailable("Wireless scan tool (nmcli) is not installed on this host.");
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCommandAsync(
        string fileName,
        IReadOnlyList<string> arguments,
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
        timeoutCts.CancelAfter(ScanTimeout);

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
