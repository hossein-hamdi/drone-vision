# Gateway & Orchestration Integration Contract

**Feature**: `001-drone-network-checker` | **Date**: 2026-11-22

---

## 1. Ocelot Gateway Route (`src/gateway/ocelot.checker.json`)

Follows the exact convention documented in `src/gateway/GEMINI.md` and `DOCKER.md`:

```json
{
  "Routes": [
    {
      "DownstreamHostAndPorts": [
        {
          "Host": "checker",
          "Port": 8080
        }
      ],
      "DownstreamScheme": "http",
      "DownstreamPathTemplate": "/api/checker/{everything}",
      "Priority": 10,
      "UpstreamHttpMethod": [],
      "UpstreamPathTemplate": "/api/checker/{everything}"
    }
  ]
}
```

### Boundary Guarantees Verified Against `src/gateway/Program.cs`
- `servicePrefixes` automatically extracts `/api/checker` from `UpstreamPathTemplate`.
- `Priority: 10` takes precedence over `ocelot.frontend.json` (`Priority: 0`).
- In non-Development environments (`!app.Environment.IsDevelopment()`), requests to `/api/checker/swagger*` are rejected with `404 Not Found` at the Gateway boundary AND Swagger middleware is not registered inside `Checker.API`.
- `X-Forwarded-Proto` and `X-Forwarded-For` are overwritten by the Gateway so `Checker.API` forwarded headers and IP rate limiting cannot be spoofed by clients.
- `Authorization: Bearer <jwt>` passes through transparently to `Checker.API`, which validates the signature, issuer, audience, algorithm (`HS512`), expiration (`ClockSkew = TimeSpan.Zero`), and claims (`sub`, `sid`, `jti`).

---

## 2. Container & Compose Contract

### `src/services/Checker/compose.yaml`
```yaml
services:
  checker:
    image: drone-analyze-checker:production
    build:
      context: .
      dockerfile: Dockerfile
      target: runtime
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      Gateway__PathBase: /api/checker
      Jwt__Issuer: ${JWT_ISSUER:-DroneVision}
      Jwt__Audience: ${JWT_AUDIENCE:-DroneVision.Client}
      Jwt__SigningKey: ${JWT_SIGNING_KEY:?Set JWT_SIGNING_KEY in .env}
    expose:
      - "8080"
    restart: unless-stopped
```

### Root Workspace & Compose Updates
- `compose.yaml`: add `- ./src/services/Checker/compose.yaml` under `include:`.
- `compose.dev.yaml`: add `checker` override (`image: drone-analyze-checker:development`, `target: development`, `ASPNETCORE_ENVIRONMENT: Development`, `develop.watch` syncing `./src/services/Checker` -> `/src` excluding `**/bin/`, `**/obj/`, `Dockerfile`).
- `src/gateway/compose.yaml`: add `checker` to `depends_on:`.
- `pnpm-workspace.yaml`: add `src/services/Checker` under `packages:`.
