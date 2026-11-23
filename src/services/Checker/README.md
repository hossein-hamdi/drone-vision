# Checker Service (`src/services/Checker`)

Standalone `.NET 10.0` Clean Architecture microservice that inspects wireless station BSSIDs/SSIDs against curated IEEE drone OUI/MA-M/MA-S blocks and SSID patterns, enumerates host WLAN interfaces, and scans across all usable WLAN adapters to return deduplicated drone detections.

## Endpoints

| Method & Path | Purpose | Windows Docker Behavior |
| --- | --- | --- |
| `GET /networks/check?bssid=...&ssid=...` | Single-station lookup returning 9 streamlined fields | Works normally (`200 OK`) |
| `GET /networks/interfaces` | Lists host 802.11 WLAN adapters & scan availability | Returns `200 OK` with `available: false`, `interfaces: []` |
| `GET /networks/scan?includePossible=false` | Scans all usable WLAN interfaces, deduplicates by BSSID, filters drones | Safely returns `503 Service Unavailable` (`ProblemDetails`) |
| `GET /health` | Unauthenticated liveness/readiness check | Works normally (`200 OK`) |

## Docker Deployment (Windows vs. Linux)

- **Default (`compose.yaml`)**: Uses standard Docker bridge networking (`http://checker:8080` for Ocelot Gateway) and non-root `USER $APP_UID`. Safe on both Windows Docker Desktop and Linux Docker.
- **Linux Hosts with Wi-Fi Hardware**: `Dockerfile` pre-installs `iw`, `network-manager` (`nmcli`), and `libcap2-bin`. Combine with `compose.linux-wifi.yaml` when mounting `/run/dbus/system_bus_socket` from a NetworkManager-enabled Linux host, or run with host networking + `NET_ADMIN` for direct `iw` scanning.

## Local Commands

```powershell
dotnet test Checker.Tests/Checker.Tests.csproj
dotnet format whitespace . --folder --verify-no-changes
```
