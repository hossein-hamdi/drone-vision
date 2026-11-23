# Feature Specification: Multi-WLAN Drone Scanning & Streamlined Network Responses

**Feature Branch**: `002-multi-wlan-scan`

**Created**: 2026-11-24

**Status**: Draft

**Input**: User description: "we have new thing to impleiment out plan is liek this use click on networ check you check on all the possible wlans interfaces get them all and filter on them (do not dublicate) --important not = we know that in the windows docker it is not possible so simple return sercies not avalibe on windows our something liek this but on linux you shoud do that and install depencey for it in you container i think somethin like netsq or i don not know - also impeliemtn a get endpoint to get the wlans interfaces and alse in you /check ednpoint and the other one remove unnesery infoes we do not need that much infoeds just keep the important ones please and ye good luck so it is importnat to safley impeliment it in windows docker and linux docker"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Multi-WLAN Drone Scanning with Deduplication (Priority: P1)

When an authorized user triggers a live drone network check scan, the service discovers all available wireless (WLAN) interfaces on the host, scans nearby wireless networks across every usable interface, combines and deduplicates observations so the same station never appears twice, and filters the results to return only detected drone stations.

**Why this priority**: Scanning across all available WLAN interfaces without returning duplicate entries is the core functional goal of the feature, ensuring multi-adapter hosts detect drones across every active radio band while keeping results clean.

**Independent Test**: Can be fully tested by executing live network scans across simulated or real hosts with multiple WLAN interfaces observing overlapping and unique wireless stations (both drone and non-drone), verifying that detections from all interfaces are captured, non-drone stations are filtered out, and duplicate hardware addresses (BSSIDs) are consolidated into a single record.

**Acceptance Scenarios**:

1. **Given** an authenticated user on a host with multiple active WLAN interfaces (for example, one scanning 2.4 GHz and another scanning 5 GHz), **When** they trigger a live network scan, **Then** the system scans all usable WLAN interfaces, aggregates observed networks from every interface, filters out non-drone networks, and returns each detected drone station once.
2. **Given** two or more WLAN interfaces observe the same drone station hardware address (BSSID) during the same scan, **When** the scan consolidates results, **Then** the system deduplicates the observations into a single drone entry preserving the strongest signal measurement, complete non-empty station details (such as a captured SSID), and the set of interfaces on which it was observed.
3. **Given** a host has multiple WLAN interfaces where one interface is unavailable, disabled, or fails to scan while at least one other interface scans successfully, **When** a live network scan runs, **Then** the system safely skips the failed interface without crashing or aborting the scan and returns deduplicated drone detections from the healthy interfaces.
4. **Given** all usable WLAN interfaces complete scanning and no drone stations are detected, **When** results are returned, **Then** the system returns an empty drone collection cleanly with accurate scan summary metadata.

---

### User Story 2 - Safe Containerized Execution Across Windows Docker and Linux Docker (Priority: P1)

Platform operators deploy the service safely in both Windows Docker and Linux Docker environments: the Linux container image includes the required wireless scanning dependencies so multi-interface discovery and scanning work out of the box on Linux hosts with wireless hardware access, while environments lacking wireless hardware access (specifically Windows Docker containers) fail gracefully with a clear Service Unavailable response without crashing, hanging, or blocking other capabilities. Native hosts (both Linux and Windows outside containers) continue supporting live scanning when local wireless adapters are accessible.

**Why this priority**: Reliable, non-crashing behavior across Windows Docker, Linux Docker, and native hosts ensures Windows Docker deployments fail safely while Linux containers and local native developer setups operate smoothly.

**Independent Test**: Can be tested independently by running live scan requests in an environment without wireless hardware access (simulating Windows Docker), on a native Windows or Linux host with wireless hardware, and in a Linux container with wireless scanning tooling installed, verifying graceful Service Unavailable handling when hardware is absent and working multi-WLAN scanning when hardware is available.

**Acceptance Scenarios**:

1. **Given** the service runs inside a Windows Docker container (or any container or host lacking accessible wireless interfaces or scanning tools), **When** an authorized user requests a live network scan, **Then** the service safely returns a standardized Service Unavailable response explaining that wireless scanning is unavailable in this environment, while single-station BSSID/SSID checks, WLAN interface capability checks, and health probes continue operating normally.
2. **Given** the service container image is built and deployed on a Linux host with wireless adapter access, **When** multi-interface scanning or WLAN interface discovery runs, **Then** the container already includes the required Linux wireless scanning utilities so discovery and scanning execute without manual post-deployment package installation.
3. **Given** the service runs natively on a Linux or Windows host outside Docker where local wireless adapters are present and enabled, **When** an authorized user requests a live network scan, **Then** the system enumerates all available wireless adapters on that host, scans across them, deduplicates results, and returns only matching drone stations.
4. **Given** an underlying wireless command stalls, exceeds a safe timeout, or encounters container permission limits, **When** scanning or interface discovery executes, **Then** the system terminates stalled work safely within a bounded timeout and returns a sanitized Service Unavailable response without exposing internal system commands, host paths, or stack traces.

---

### User Story 3 - Discover Available WLAN Interfaces (Priority: P2)

An authorized operator or client application queries a dedicated read-only endpoint to inspect all wireless (WLAN) network interfaces available on the host (excluding wired, loopback, and virtual non-wireless adapters) along with whether live scanning is currently available in the running environment.

**Why this priority**: Listing available WLAN interfaces and availability status lets operators and client UIs cleanly verify which wireless adapters exist or display a clear unavailable notice in Windows Docker without triggering an unexpected error.

**Independent Test**: Can be tested independently by requesting the WLAN interfaces endpoint with valid authentication against hosts with zero, one, or multiple WLAN interfaces (alongside wired/virtual interfaces) and verifying authenticated access, accurate wireless-only filtering, and clean availability reporting when no hardware is accessible.

**Acceptance Scenarios**:

1. **Given** an authenticated user on a host with one or more WLAN interfaces alongside non-wireless interfaces (such as Ethernet, loopback, or container bridges), **When** they query the WLAN interfaces endpoint, **Then** the system returns a successful response indicating scanning is available (`available: true`) and listing only the WLAN interfaces with essential attributes (such as interface identifier/name and operational state).
2. **Given** an authenticated user queries the WLAN interfaces endpoint in an environment where no wireless interfaces are accessible (such as Windows Docker) or wireless scanning tooling cannot access hardware, **When** interface discovery executes, **Then** the system returns a successful response containing an empty interfaces list, an availability indicator set to unavailable (`available: false`), and a clear human-readable reason message explaining why scanning is unavailable, without failing the request.
3. **Given** an unauthenticated user or a request with unexpected query parameters reaches the WLAN interfaces endpoint, **When** the request is processed, **Then** the system rejects it using the existing standard unauthorized or validation error responses.

---

### User Story 4 - Streamlined Network Check and Scan Responses (Priority: P2)

Authorized users querying either the single-station network check endpoint or the live multi-WLAN scan endpoint receive concise, balanced responses containing only the essential decision, drone identification, operating band, and live RF signal fields, with verbose internal catalog and redundant diagnostic fields removed.

**Why this priority**: Removing unnecessary internal and diagnostic metadata keeps responses focused, lightweight, and directly aligned with what operators and client interfaces display.

**Independent Test**: Can be tested independently by issuing valid requests to both the single-station check endpoint and the live scan endpoint and verifying that the retained balanced field set is present and all removed verbose fields are absent.

**Acceptance Scenarios**:

1. **Given** an authenticated user submits a valid BSSID (and optional SSID) to the single-station check endpoint, **When** the evaluation completes, **Then** the response returns only the essential fields: drone-station boolean flag, detection verdict, confidence score, normalized BSSID, observed SSID, brand mark, candidate models, operating frequency bands, and station role—omitting internal OUI/prefix diagnostics, locally-administered/multicast flags, chipset exclusivity, hardware vendor, frequency ranges, channel summaries, radio protocols, signal range tiers, known SSID pattern lists, match source, and verbose notes.
2. **Given** an authenticated user invokes a live multi-WLAN scan that detects drone networks, **When** results are returned, **Then** each detected drone entry includes only the essential identification fields from the check response plus live RF/interface metrics (signal strength percentage, RSSI in dBm, active band, frequency in MHz, channel, and observing interface names), omitting authentication mode, radio type, internal OUI/prefix diagnostics, protocols, ranges, and verbose notes.

---

### Edge Cases

- What happens when two WLAN interfaces detect the same BSSID, where one interface captured the SSID and the other saw an empty/hidden SSID? The deduplication step merges them into a single station record, retaining the non-empty SSID, the strongest signal observation, and both interface identifiers.
- What happens when two distinct BSSIDs broadcast the same SSID (for example, multiple aircraft or dual-band access points from the same model)? They remain separate station entries because deduplication is keyed by unique wireless station address (BSSID), not SSID alone.
- What happens when a Linux host or container has wireless adapters that are administratively down, RF-blocked, or lack scan privileges? Non-usable adapters are safely excluded from failing healthy adapters; if no usable wireless adapter can scan or permissions/tooling prohibit scanning, live scanning returns a sanitized Service Unavailable response while WLAN interface discovery safely reports zero usable adapters or unavailable status (`available: false`) with a sanitized reason.
- What happens when a scan command produces malformed, partial, or unparseable output on one WLAN interface? Unparseable lines or invalid BSSID entries are safely skipped without crashing the service or dropping valid detections from other interfaces.
- What happens when running inside Windows Docker and a user performs a single-station BSSID/SSID lookup or queries the WLAN interfaces endpoint? Single-station lookups remain completely functional using the embedded signature catalog, and the WLAN interfaces endpoint succeeds with an empty list and `available: false` plus a clear reason, while live scanning safely returns Service Unavailable.
- What happens when callers send unexpected query parameters or burst requests to the WLAN interfaces endpoint or updated check/scan endpoints? All endpoints enforce the same strict query allowlisting, authentication, rate limiting, and defensive headers as existing endpoints.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST enumerate all wireless (WLAN) network interfaces available on the host while filtering out wired, loopback, virtual bridge, and other non-wireless interfaces.
- **FR-002**: System MUST expose a read-only authenticated endpoint (HTTP GET) returning an availability status (`available` boolean and optional human-readable `reason`) alongside the list of discovered WLAN interfaces and their essential status metadata; when no wireless interfaces or scanning hardware are accessible (such as in Windows Docker), the endpoint MUST return a successful response with an empty interface list, `available: false`, and an explanatory reason rather than failing.
- **FR-003**: System MUST scan nearby wireless networks across all usable discovered WLAN interfaces during a live network scan, aggregate observations across interfaces, filter results to matching drone stations (preserving existing `includePossible` filtering semantics), and deduplicate stations so each unique canonical BSSID appears at most once.
- **FR-004**: When consolidating duplicate observations of the same BSSID across multiple WLAN interfaces, the system MUST keep a single record retaining the strongest observed signal measurement, the richest non-empty station attributes (such as SSID, band, frequency, and channel), and the deduplicated list of observing interface identifiers.
- **FR-005**: System MUST operate safely across Windows Docker, Linux Docker, and native Windows/Linux hosts:
  - Live multi-interface scanning MUST remain supported on both Linux (Docker and native) and native Windows hosts when accessible wireless hardware exists.
  - When deployed in any environment without accessible wireless interfaces or scanning capability (including Windows Docker containers), live scanning MUST fail gracefully with a standardized, sanitized Service Unavailable response without crashing or hanging, while non-hardware endpoints continue functioning normally.
  - The Linux container build definition MUST pre-install the required Linux wireless scanning runtime dependencies so multi-interface WLAN discovery and scanning work in Linux Docker deployments with wireless access without manual post-build installation.
- **FR-006**: System MUST execute host wireless discovery and scanning safely with bounded timeouts, clean cancellation/termination of timed-out operations, protection against command injection, and sanitized error responses that never disclose internal system details.
- **FR-007**: System MUST streamline response payloads across both the single-station check endpoint and the live network scan endpoint:
  - Single-station check results MUST retain only: drone-station indicator, verdict, confidence score, canonical BSSID, observed SSID, brand mark, candidate models, operating frequency bands, and station role.
  - Scanned drone entries MUST retain only those same identification fields plus live signal/channel/interface observations: signal strength percentage, RSSI (dBm), active band, frequency (MHz), channel, and observing interface names.
  - All redundant/diagnostic attributes (OUI prefix, matched prefix, locally administered flag, multicast flag, matched SSID pattern, hardware vendor, chipset exclusivity, match source, frequency ranges, channel summary, radio protocols, signal range tier, known SSID patterns, authentication type, radio type, and notes) MUST be removed from these payloads.
- **FR-008**: System MUST apply existing authentication, rate limiting, strict query-parameter allowlisting, defensive security headers, gateway path-base routing, and development-only API documentation controls consistently across the new WLAN interfaces endpoint and updated check/scan endpoints.

### Key Entities *(include if feature involves data)*

- **WlanInterface**: Represents a wireless network interface discovered on the host, including interface identifier/name, operational state (e.g., ready/connected/disconnected/unavailable), and whether it is usable for scanning.
- **WlanInterfacesAvailability**: Represents the host WLAN discovery outcome containing availability flag (`available`), optional explanatory `reason`, and the list of discovered `WlanInterface` items.
- **ScannedWiFiObservation**: Represents a raw wireless network observation gathered from a specific WLAN interface during a live scan (BSSID, optional SSID, signal strength, RSSI, band, frequency, channel, and source interface name), merged during cross-interface deduplication.
- **StreamlinedCheckResult**: Represents the slimmed-down single-station lookup outcome containing only the 9 essential verdict, identity, frequency band, and station role attributes.
- **StreamlinedScannedDrone**: Represents a deduplicated, drone-filtered scan result entry containing only the 9 core identification attributes plus live RF signal, channel, and observing interface names.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a host with two or more active WLAN interfaces observing overlapping wireless networks, a live scan captures stations from 100% of usable WLAN interfaces and produces 0 duplicate BSSID entries in the filtered drone response.
- **SC-002**: 100% of non-wireless host interfaces (wired Ethernet, loopback, virtual bridges) are excluded from WLAN interface discovery results.
- **SC-003**: In a Windows Docker environment (lacking host Wi-Fi passthrough), 100% of WLAN interface discovery requests succeed cleanly with an empty list and `available: false`, while 100% of live scan requests return a sanitized Service Unavailable response within the bounded timeout without disrupting non-scan lookups.
- **SC-004**: In a Linux container built from the updated container definition, WLAN interface discovery and multi-interface scanning run using pre-installed dependencies with zero manual container configuration or package installation at runtime.
- **SC-005**: Response field count is reduced by more than 60% on single-station check results (from 23 fields to 9 fields) and by more than 40% on each scanned drone entry (from 26 fields to 15 fields, including multi-interface attribution) while preserving 100% of essential drone identification and live RF signal attributes.
- **SC-006**: 100% of unauthorized, malformed, or unexpected-query requests across the WLAN interfaces, network check, and network scan endpoints are rejected cleanly under existing security and rate-limiting rules.

## Assumptions

- **Linux Container Wireless Hardware Access**: Live scanning inside a Linux Docker container assumes the container is deployed on a Linux host with wireless hardware and appropriate container network/device access; where container isolation prevents wireless access (such as Windows Docker Desktop), graceful Service Unavailable handling on scan and `available: false` on interface discovery apply.
- **Preserved Detection Logic**: Core BSSID normalization, IEEE OUI / MA-M / MA-S prefix matching, shared-chipset conservative scoring, SSID pattern rules, and `includePossible` scan filtering behavior established in `001-drone-network-checker` remain unchanged internally even though verbose diagnostic fields are omitted from HTTP responses.
- **Gateway & Auth Continuity**: All new and updated endpoints operate behind the existing platform gateway route prefix using the existing dual browser-session and bearer JWT authentication rules.
