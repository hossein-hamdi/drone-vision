# Feature Specification: Drone Network Checker Service

**Feature Branch**: `001-drone-network-checker`

**Created**: 2026-11-22

**Status**: Draft

**Input**: User description: "we need a nework checker for drones in c# it get the bssid and check that are they drone stations or not for that you should gather famouse ssid out mac adress patterns for exmplae syma mjx bugs dji everythings and after that i expect ypu the give me ther result of that networks like stench bound in withc ferequnes dorne model mark and this kind of things you will restund them via a get request form api add that api to gateway with authnticaion of jwt (see manamgent system jwt config) - this network checker is a seperate service so i exepct yout ot create its own dockerfile (keep codes clrean and readable use common desgin patter for c# and setup swagger for it) also impeliment secutriy for it please that is important"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Identify Drone Stations by BSSID and Optional SSID (Priority: P1)

An authorized operator or client system submits a wireless station BSSID (and optionally an observed network SSID) to a read-only lookup capability to determine whether the station belongs to a drone, drone controller, or drone-associated radio system, receiving structured identification intelligence.

**Why this priority**: Determining whether an observed wireless network belongs to a drone and identifying its brand, model family, and operating frequencies is the core purpose of the service and delivers immediate standalone value.

**Independent Test**: Can be tested independently by querying the read-only lookup interface with known drone BSSIDs (such as DJI, Parrot, Autel, Yuneec, Skydio, Hubsan), shared chipset BSSIDs paired with famous drone SSIDs (such as Syma, MJX, Bugs, Ryze Tello, Holy Stone), and ordinary non-drone BSSIDs, and verifying the returned verdict, confidence level, brand mark, models, frequency bands, and pattern match details.

**Acceptance Scenarios**:

1. **Given** an authenticated user submits a valid BSSID matching a dedicated drone manufacturer prefix (such as a DJI, Parrot, Autel, Yuneec, or Skydio station), **When** the lookup query executes, **Then** the system identifies the network as a drone station with high confidence and returns the manufacturer mark, candidate drone models/series, operating frequency bands and frequency ranges, radio transmission profile, station role, matched address prefix, and associated known SSID patterns.
2. **Given** an authenticated user submits a valid BSSID belonging to a shared consumer Wi-Fi chipset alongside an observed drone SSID pattern (such as Syma, MJX, Bugs, Tello, or Holy Stone), **When** the lookup query executes, **Then** the system correlates both signals, identifies the specific drone brand mark and model series, indicates that both chipset prefix and SSID matched, and returns the corresponding operating frequency bands and radio profile.
3. **Given** an authenticated user submits a valid BSSID belonging to a shared Wi-Fi chipset commonly used in toy or hobby drones without an SSID, **When** the lookup query executes, **Then** the system avoids falsely confirming it as a dedicated drone and reports a low-confidence / unconfirmed chipset indicator noting that SSID correlation is needed for confirmation.
4. **Given** an authenticated user submits a valid BSSID belonging to an ordinary non-drone vendor or an unknown address prefix (with no matching drone SSID), **When** the lookup query executes, **Then** the system returns a clear negative result indicating the station is not recognized as a known drone network.
5. **Given** a user submits a BSSID in any standard MAC address format (colon-separated, hyphen-separated, dot-separated, bare hexadecimal, or mixed case), **When** the lookup query executes, **Then** the system normalizes the address consistently and produces identical identification results.

---

### User Story 2 - Gateway Access & Management JWT Security Enforcement (Priority: P1)

An authenticated platform user accesses the drone network checker through the unified platform gateway using a JWT bearer access token issued by the Management system, while unauthenticated, tampered, expired, abusive, or malformed requests are rejected with standardized, non-leaking responses.

**Why this priority**: Security and unified gateway routing are critical requirements; exposing an unauthenticated or unhardened service would bypass platform access controls and introduce security exposure.

**Independent Test**: Can be tested independently by sending lookup requests through the platform gateway with valid Management JWT tokens, expired/forged/unsigned tokens, missing credentials, malformed inputs, and burst traffic, verifying that only valid authenticated requests succeed and blocked requests return safe, standardized error payloads without exposing internal details.

**Acceptance Scenarios**:

1. **Given** a client sends a lookup request through the platform gateway under the checker service route with a valid, active JWT bearer token issued by the Management system, **When** the gateway routes the request and the checker validates the token, **Then** the request is authorized and returns the drone identification result.
2. **Given** a client sends a request with a missing, malformed, expired, wrong-issuer, wrong-audience, untrusted-algorithm, or tampered token (or a token missing required identity and session claims), **When** the request reaches the service, **Then** the service rejects it as unauthorized without revealing cryptographic details, internal configuration, or stack traces.
3. **Given** a client submits malformed BSSID values, oversized SSID values, or invalid query inputs, **When** the service validates input, **Then** the request is rejected immediately as invalid input with a standardized problem response and safe error message.
4. **Given** a client sends an excessive burst of requests exceeding configured rate limits, **When** the threshold is exceeded, **Then** subsequent requests are throttled to protect service availability.

---

### User Story 3 - Comprehensive Drone Signature Catalog & Safe Documentation Discovery (Priority: P2)

Security analysts and integrators rely on a comprehensive, well-structured catalog of famous drone manufacturer OUI/MAC address prefixes and SSID naming patterns across major ecosystems (including Syma, MJX, Bugs, DJI, Parrot, Hubsan, Autel, Yuneec, Skydio, Ryze Tello, Holy Stone, Potensic, Eachine, JJRC, Walkera, and others) and can browse interactive API documentation in development while keeping documentation inaccessible in production.

**Why this priority**: Broad coverage of both dedicated drone manufacturers and Wi-Fi-based hobby drones directly drives detection usefulness, while environment-gated documentation streamlines integration without expanding production attack surface.

**Independent Test**: Can be tested by querying representative signatures across major drone ecosystems (verifying accurate frequency bands, brand marks, and models) and verifying interactive documentation loads in development environments and is unreachable in production environments.

**Acceptance Scenarios**:

1. **Given** the checker service initializes with its drone signature catalog, **When** lookups are performed across diverse drone brands (including DJI, Syma, MJX, Bugs, Parrot, Autel, Hubsan, Yuneec, Skydio, Ryze Tello, Holy Stone, and Potensic), **Then** each brand's address prefixes and/or SSID patterns resolve to accurate brand marks, representative drone models, operating frequency bands, and radio classifications.
2. **Given** the service and gateway are running in a development environment, **When** an integrator opens the interactive API documentation through the gateway or directly, **Then** the documentation displays operation details, parameters, response schemas, and bearer token authentication support.
3. **Given** the service and gateway are running in a production environment, **When** any client requests the interactive API documentation directly or through the gateway, **Then** access is blocked and treated as not found.

---

### User Story 4 - Standalone Service Isolation & Containerized Operations (Priority: P3)

Platform operators deploy, monitor, and operate the drone network checker as an independent containerized service alongside the existing gateway and Management services without shared database dependencies or tightly coupled release lifecycles.

**Why this priority**: Operating as an isolated, self-contained service enforces clean service boundaries, independent scaling, and reduced operational blast radius.

**Independent Test**: Can be tested independently by building and launching the standalone service container alongside the gateway, verifying health checks report ready under non-privileged execution, and performing authenticated lookups through the gateway.

**Acceptance Scenarios**:

1. **Given** the standalone checker service container is built and launched with required runtime token-validation settings, **When** the service starts, **Then** it validates security configuration on startup, initializes without depending on Management's data store, and runs under a non-privileged identity.
2. **Given** an operator attempts to start the service with a missing or cryptographically weak token-signing key configuration, **When** startup validation runs, **Then** the service refuses to start and reports a clear configuration failure rather than operating insecurely.

---

### Edge Cases

- What happens when the caller supplies a locally administered or randomized MAC address (where the locally administered address bit is set)? The system detects and flags the locally administered indicator, avoids high-confidence attribution based solely on the randomized address, and still evaluates any supplied SSID pattern.
- What happens when a BSSID is all-zeros, broadcast, wrong length, or contains non-hexadecimal characters? The system rejects the request immediately with a clear validation error response.
- What happens when an authenticated caller supplies a valid BSSID without an SSID and the manufacturer prefix spans multiple drone models or both aircraft and remote controllers (such as DJI)? The system returns the confirmed brand mark, applicable station role possibilities, candidate model families, and combined operating frequency bands rather than guessing a single narrow model.
- What happens when a caller supplies an SSID exceeding maximum wireless naming bounds (more than 32 bytes/characters), containing control characters, or crafted to trigger slow pattern evaluation? The system rejects invalid or oversized inputs during validation and guarantees bounded, predictable pattern evaluation time.
- What happens when a caller submits requests to unrecognized routes or uses non-read HTTP methods on the lookup resource? Both the gateway and the service reject the request with standardized non-leaking error responses and defensive security headers.
- What happens when a caller expects signal strength or frequency details from a BSSID-only lookup? Because real-time received signal strength is an environmental measurement rather than an intrinsic property of a BSSID, the system returns the drone family's known operating frequency bands, frequency ranges, radio protocol, and typical transmission power/range tier.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a read-only network lookup capability (HTTP GET) accepting a required wireless station BSSID and an optional network SSID to evaluate whether the station is a drone network.
- **FR-002**: System MUST normalize valid BSSID inputs across standard MAC notations (colon-separated, hyphen-separated, dot-separated, and bare 12-character hexadecimal, case-insensitive) into a single canonical format before evaluation, and MUST reject syntactically invalid, empty, all-zero, or broadcast addresses with a validation error response.
- **FR-003**: System MUST maintain a structured, extensible signature catalog covering:
  - Dedicated drone manufacturer MAC/OUI address prefixes.
  - Shared Wi-Fi chipset MAC/OUI prefixes commonly used inside consumer and hobby drones (clearly marked as shared chipsets).
  - Famous drone SSID naming patterns (prefix, exact, and bounded pattern rules) covering major brands including at minimum DJI, Syma, MJX, Bugs, Parrot, Hubsan, Autel Robotics, Yuneec, Skydio, Ryze/Tello, Holy Stone, Potensic, Eachine, JJRC, Cheerson, Walkera, 3DR, and Hover/Zero Zero Robotics.
- **FR-004**: System MUST evaluate MAC/OUI prefixes and SSID patterns together (when an SSID is provided) to produce a deterministic verdict (`Confirmed`, `Probable`, `Possible`, or `NotRecognized`) along with a boolean drone-station indicator, match source (`OUI`, `SSID`, or `OUI+SSID`), and confidence level/score.
- **FR-005**: System MUST NOT confirm a station as a drone solely from a shared general-purpose Wi-Fi chipset prefix when no corroborating drone SSID is supplied; instead, it MUST indicate an unconfirmed shared-chipset match requiring SSID correlation.
- **FR-006**: System MUST return comprehensive station metadata for recognized networks, including:
  - Detection verdict, confidence level/score, and match source
  - Manufacturer / brand mark
  - Candidate drone model(s) or product family/series
  - Station category/role (e.g., Aircraft Access Point, Ground Controller/Remote, FPV/Telemetry Module, or Unknown)
  - Operating frequency bands (such as 2.4 GHz, 5 GHz, 5.8 GHz) and detailed frequency ranges / channel spectrum notes
  - Radio link standard and typical transmission power/range tier
  - Matched OUI prefix, locally administered address flag, and known/matched SSID pattern details.
- **FR-007**: System MUST be integrated behind the platform API gateway under a dedicated service route prefix prioritized above catch-all frontend routing, upholding all existing gateway boundary protections (route prefix allowlisting, forwarding-header sanitization, and production documentation lockout).
- **FR-008**: System MUST authenticate every lookup request using bearer JWT tokens issued by the Management identity system, enforcing Management's exact token rules: matching trusted issuer, matching trusted audience, matching signing key, strict signature algorithm pinning matching Management's token configuration, zero expiration clock skew, mandatory signed tokens with expiration, and presence of valid subject, session, and token identifier claims.
- **FR-009**: System MUST validate at startup that token-validation settings (issuer, audience, and a signing key meeting Management's minimum cryptographic strength of at least 64 random bytes) are present and valid, failing fast on startup if misconfigured.
- **FR-010**: System MUST enforce defensive API security controls across all endpoints:
  - Strict input validation rejecting malformed BSSIDs, oversized SSIDs (above 32 bytes/characters), control characters, and unexpected parameters
  - Time-bounded pattern matching resilient against pattern-evaluation denial of service
  - Per-client rate limiting that throttles excessive request bursts
  - Defensive response security policies preventing content-type sniffing and caching of responses, and enforcing encrypted transport in production
  - Standardized, sanitized error responses for all failures that omit stack traces, cryptographic failure causes, and internal host/configuration details.
- **FR-011**: System MUST expose interactive API documentation (with bearer token testing support and gateway-aware paths) only in development environments, and MUST disable and block documentation routes in production.
- **FR-012**: System MUST run as an independent, self-contained containerized service with its own container build definition, non-privileged runtime execution, a liveness/readiness health check reporting operational status without exposing sensitive internals, and zero dependency on Management's database.

### Key Entities *(include if feature involves data)*

- **DroneSignatureEntry**: A catalog record representing a known drone manufacturer OUI/MAC prefix or drone ecosystem profile, capturing address prefix(es), exclusivity classification (dedicated drone vendor vs. shared chipset), brand mark, candidate models/series, station role, supported frequency bands and frequency ranges, radio standard, typical signal/range tier, and associated SSID conventions.
- **SsidPatternRule**: A pattern rule associating famous drone Wi-Fi SSID naming conventions with a brand mark, drone model family, operating frequency bands, radio profile, and match confidence weight.
- **NetworkCheckQuery**: The validated lookup request containing a normalized BSSID (plus extracted OUI prefix and locally administered flag) and an optional normalized SSID.
- **NetworkCheckResult**: The structured lookup output containing the detection verdict (`isDroneStation`, classification, confidence score/tier, match source), manufacturer mark, candidate models, station role, operating frequency bands and frequency ranges, radio/signal range tier, matched prefix/SSID indicators, and explanatory notes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 95% of test cases drawn from a representative validation set of documented drone OUIs and famous drone SSIDs (covering DJI, Syma, MJX, Bugs, Parrot, Autel, Hubsan, Yuneec, Skydio, Ryze Tello, Holy Stone, and Potensic) resolve accurately to the expected brand mark, model family, and operating frequency bands.
- **SC-002**: 100% of tested BSSIDs belonging solely to general-purpose or non-drone hardware without a corroborating drone SSID produce zero false-positive confirmed drone detections.
- **SC-003**: 100% of unauthenticated, expired, wrong-issuer, wrong-audience, tampered, missing-claim, or weak-algorithm access token requests are rejected as unauthorized without disclosing cryptographic or internal validation details.
- **SC-004**: 100% of malformed BSSID or invalid SSID requests are rejected immediately as validation failures, and request bursts exceeding configured thresholds are throttled cleanly.
- **SC-005**: Single station lookup requests complete in under 50 milliseconds under normal operating load once the service is initialized.
- **SC-006**: Interactive API documentation is accessible and usable via the gateway in development mode and completely unreachable (blocked both directly and through the gateway) in production mode.
- **SC-007**: The standalone containerized service builds cleanly, passes readiness checks within 30 seconds of startup, runs under a non-privileged user, and serves authenticated lookups end-to-end through the platform gateway.

## Assumptions

- **Gateway Pass-Through Authentication Boundary**: Following the existing platform gateway architecture, the gateway handles routing, prefix allowlisting, header sanitization, and production documentation lockout, while JWT validation, rate limiting, and input validation are executed inside the Checker service itself.
- **Stateless JWT Verification**: Checker validates Management-issued bearer tokens statelessly using shared issuer, audience, and signing-key configuration so that Checker remains cleanly decoupled from Management's database. Any user holding a valid non-expired access token issued by Management is permitted to query Checker; per-request database session revocation checks or fine-grained RBAC permissions are out of scope for the initial release.
- **Offline Curated Knowledge Base**: The drone OUI and SSID catalog is maintained as a curated, structured in-service knowledge base derived from publicly documented IEEE OUI registrations and known consumer/prosumer/FPV drone SSID conventions, requiring no live internet access during lookups.
- **Frequency & Signal Metadata Semantics**: When queried by BSSID (and optional SSID) alone, frequency band and radio/signal output reflect the matched drone family's documented operating bands, frequency ranges, radio standard, and typical transmission power/range classification rather than live RF sensor measurements.
- **Service Isolation & Maintainability**: Technology stack, clean modular design patterns, and container implementation specifics will be finalized during technical planning to align cleanly with the existing backend service conventions in this repository.
