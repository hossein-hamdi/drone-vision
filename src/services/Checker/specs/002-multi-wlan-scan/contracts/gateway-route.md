# Gateway & Multi-Platform Container Deployment Contract

**Feature**: `002-multi-wlan-scan` | **Date**: 2026-11-24

---

## 1. Gateway Compatibility (`src/gateway/ocelot.checker.json`)

No gateway route edits are required. The existing catch-all service route:
- `UpstreamPathTemplate`: `/api/checker/{everything}`
- `DownstreamPathTemplate`: `/api/checker/{everything}`
- `DownstreamHostAndPorts`: `checker:8080`

automatically routes:
- `GET /api/checker/networks/check`
- `GET /api/checker/networks/interfaces` *(new)*
- `GET /api/checker/networks/scan`
- `GET /api/checker/health`

All boundary guarantees (`Priority: 10`, `Gateway__PathBase=/api/checker`, forwarded headers sanitization, dual auth passthrough, production `/swagger` 404 lockout) apply equally to `/networks/interfaces`.

---

## 2. Container Runtime Safety (`Dockerfile` & `compose.yaml`)

### A. `src/services/Checker/Dockerfile`
Both `development` and `runtime` stages run on Debian-based .NET 10 images and MUST include Linux wireless scanning dependencies:
- Install `iw`, `network-manager` (`nmcli`), and `libcap2-bin` (`apt-get update && apt-get install -y --no-install-recommends iw network-manager libcap2-bin && rm -rf /var/lib/apt/lists/*`).
- Grant Linux file capabilities `cap_net_admin,cap_net_raw+ep` to `/usr/sbin/iw` (best-effort) while retaining non-root `USER $APP_UID` in the `runtime` stage.
- Do **not** start a `NetworkManager` daemon inside the container; `iw` talks directly to kernel `nl80211` and `nmcli` talks optionally to a bind-mounted host D-Bus socket when configured.

### B. Safe Default Compose (`src/services/Checker/compose.yaml`) — Works on Windows Docker & Linux Docker
`src/services/Checker/compose.yaml` MUST remain on the standard Docker Compose bridge network without `network_mode: host` or `privileged: true` so:
1. `gateway -> http://checker:8080` DNS routing always resolves cleanly.
2. **Windows Docker (Docker Desktop / WSL2)**: Container starts normally as non-root `$APP_UID`; `/networks/interfaces` returns `200 OK { "available": false, "interfaces": [] }`; `/networks/scan` returns `503 Service Unavailable`; `/networks/check` and `/health` remain 100% functional.

### C. Linux Wireless Scanning Deployment Options (Linux Hosts Only)
Operators deploying on a Linux host with physical WLAN adapters choose one of two safe runtime attachments (documented in an optional Linux override e.g. `compose.linux-wifi.yaml` or deployment notes):
1. **Option 1 — Host NetworkManager D-Bus Socket Mount (Preserves Docker Bridge DNS for Gateway)**:
   Mount `/run/dbus/system_bus_socket:/run/dbus/system_bus_socket:ro` (or rw if rescan D-Bus policies require) on `checker` while staying on the Compose bridge network. `nmcli` enumerates and scans all host WLAN interfaces while `gateway -> checker:8080` continues working unchanged.
2. **Option 2 — Host Network + `NET_ADMIN` Capability (`iw` Direct Kernel `nl80211`, No NetworkManager Required)**:
   Run `checker` with `network_mode: host` and `cap_add: [NET_ADMIN, NET_RAW]` (applicable on native Linux Docker setups where gateway also targets `localhost:8080` or `host.docker.internal`). Non-root `$APP_UID` invokes `/usr/sbin/iw` directly across all `/sys/class/net/*/wireless` interfaces.
