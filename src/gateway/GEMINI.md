# GEMINI.md — API Gateway (`src/gateway`)

## 1. Tech Stack & Workspace Overview
The API Gateway is an ASP.NET Core `.NET 10.0` reverse proxy powered by **Ocelot**, acting as the single public HTTPS ingress boundary (`:8443`) for both the Next.js `frontend` and backend microservices (`Management`).

- **Target Framework:** `.NET 10.0` (`net10.0`, C# with `ImplicitUsings` and `Nullable` enabled)
- **Reverse Proxy:** `Ocelot` `25.0.2` (`Gateway.csproj`)
- **Workspace Integration:** `pnpm` workspace member `src/gateway` (`packageManager: pnpm@12.16.1`)
- **TLS Termination:** Kestrel HTTPS on port `8443` using `../../.certs/gateway.pfx` and `GATEWAY_CERT_PASSWORD` (see `compose.yaml`)

### Key Scripts (`src/gateway/package.json`)
| Script | Command | Purpose |
| :--- | :--- | :--- |
| `pnpm dev` | `dotnet watch run --project Gateway.csproj` | Run Ocelot gateway with hot reload |
| `pnpm build` | `dotnet build Gateway.csproj` | Compile the Gateway project |
| `pnpm start` | `dotnet run --project Gateway.csproj` | Run Gateway without watch |
| `pnpm format` | `dotnet format whitespace . --folder` | Format C# whitespace |
| `pnpm format:check` | `dotnet format whitespace . --folder --verify-no-changes` | Verify C# whitespace formatting |

---

## 2. Directory Structure
```text
src/gateway/
├── .dockerignore                            # Docker build exclusions
├── .lintstagedrc.mjs                        # Staged formatting hooks
├── compose.yaml                             # Production container config (HTTPS :8443, gateway.pfx mount)
├── Dockerfile                               # Multi-stage build (development, build, runtime on aspnet:10.0)
├── Gateway.csproj                           # .NET 10 Web SDK + Ocelot 25.0.2
├── ocelot.frontend.json                     # Catch-all frontend routes (Priority 0 -> frontend:3000)
├── ocelot.management.json                   # Management API routes (Priority 10 -> management:8080)
├── package.json                             # pnpm wrapper scripts for dotnet CLI
└── Program.cs                               # Ocelot merge, boundary guard, header sanitization, UseOcelot()
```

---

## 3. Core Files & Responsibilities
- `Program.cs`: Loads and merges all `ocelot.*.json` files in memory (`AddOcelot(builder.Environment, MergeOcelotJson.ToMemory)`), enforces `/api/*` prefix allowlisting and production Swagger blocking, sanitizes forwarding headers, and executes `await app.UseOcelot()`.
- `ocelot.management.json`: Routes `/api/management/{everything}` at `Priority: 10` over `http` to `management:8080`.
- `ocelot.frontend.json`: Routes `/` and `/{everything}` at `Priority: 0` over `http` to `frontend:3000`.
- `compose.yaml`: Mounts `../../.certs/gateway.pfx` read-only to `/certs/gateway.pfx`, sets `ASPNETCORE_URLS=https://+:8443`, and requires `GATEWAY_CERT_PASSWORD` from `.env`.

---

## 4. Code Snapshots & Gateway Conventions

### Merged Ocelot Configuration & Prefix Extraction
Gateway routes are split per downstream service (`ocelot.<service>.json`) and merged into memory at startup. `Program.cs` derives allowed `/api/<service>` prefixes dynamically from `Routes[*].UpstreamPathTemplate`:
```csharp
// CORRECT — Dynamic service prefix extraction & boundary guard (from Program.cs)
builder.Configuration.AddOcelot(builder.Environment, MergeOcelotJson.ToMemory);
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();
var servicePrefixes = builder.Configuration.GetSection("Routes").GetChildren()
    .Select(route => route["UpstreamPathTemplate"]?.Split("/{", 2)[0])
    .Where(path => path?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true)
    .Select(path => new PathString(path)).ToArray();
```

### Adding a New Downstream Service Route
Every new `/api/<service>` route **must** use a higher `Priority` than `ocelot.frontend.json` (`Priority: 0`) and preserve the `/api/<service>` prefix so `Program.cs` allowlists it automatically:
```json
// CORRECT — ocelot.<service>.json (Priority > 0, /api/<service>/ prefix)
{
  "Routes": [
    {
      "DownstreamHostAndPorts": [{ "Host": "my-service", "Port": 8080 }],
      "DownstreamScheme": "http",
      "DownstreamPathTemplate": "/api/my-service/{everything}",
      "Priority": 10,
      "UpstreamHttpMethod": [],
      "UpstreamPathTemplate": "/api/my-service/{everything}"
    }
  ]
}

// WRONG — Priority 0 collides with frontend catch-all, or omitting /api/ prefix bypasses API boundary checks
{
  "Priority": 0,
  "UpstreamPathTemplate": "/my-service/{everything}"
}
```

---

## 5. Boundary Security & Routing Invariants
- **Strict `/api/*` Allowlist:** Any request starting with `/api` that does **not** match a configured `servicePrefix` from `ocelot.*.json` is immediately terminated with `404 Not Found` before reaching `UseOcelot()` or falling through to `frontend`.
- **Production Swagger Lockout:** Requests to `<servicePrefix>/swagger` (e.g., `/api/management/swagger`) return `404 Not Found` whenever `!app.Environment.IsDevelopment()`.
- **Forwarding Header Sanitization:** `Program.cs` unconditionally overwrites `X-Forwarded-Proto` (`context.Request.Scheme`) and `X-Forwarded-For` (`context.Connection.RemoteIpAddress`) at the public boundary—never trust client-supplied forwarding headers.
- **Same-Origin Cookie & CSRF Passthrough:** The gateway terminates HTTPS on `:8443` and proxies both `frontend` (`/`) and `Management` (`/api/management/*`) on the same origin, allowing `__Secure-DroneVision.Refresh`, `__Host-DroneVision.Csrf`, and `X-CSRF-TOKEN` to flow cleanly without CORS.
- **No Business Logic or Auth Verification in Gateway:** Token validation, CSRF verification, and permission checks live in downstream services (`Management.API`), not in `src/gateway`.
