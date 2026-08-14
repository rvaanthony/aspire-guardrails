# Agent notes for this solution

Conventions for humans and coding agents working in a project stamped from this starter.

## Solution shape

| Project | Role |
|---------|------|
| `Template.AppHost` | Aspire orchestration only — no domain logic |
| `Template.ServiceDefaults` | OpenTelemetry, health, resilience, service discovery |
| `Template.Client` | ASP.NET Core MVC composition root (middleware, controllers, views) |
| `Template.Business` | Domain services and options — depends only on Common |
| `Template.Data` | Persistence implementations of Common interfaces |
| `Template.Common` | Contracts, models, exceptions — depends on nothing |
<!--#if (UseWorker)-->
| `Template.Worker` | Background service — same allowed refs as Client, no HTTP surface |
<!--#endif-->
| `*.Tests` | xUnit; Client.Tests owns architecture + HTTP smoke tests, AppHost.Tests owns resource-graph wiring |

Allowed project references, enforced twice over:

- `Common` → (none)
- `ServiceDefaults` → (none)
- `Business` → `Common`
- `Data` → `Business`, `Common`
- `Client` → `Business`, `Common`, `Data`, `ServiceDefaults`
<!--#if (UseWorker)-->
- `Worker` → `Business`, `Common`, `Data`, `ServiceDefaults`
<!--#endif-->
- `AppHost` → the hosts only
- `*.Tests` → the one project under test

`Directory.Build.targets` validates the **rank map** in `Directory.Build.props` at build time: a project missing from the map fails with `ARCH0001`, and referencing a project at the same or a higher rank fails with `ARCH0002`. `Template.Client.Tests/BuildGuardTests.cs` plants each of those violations and asserts the build rejects it, so the guard itself is tested rather than assumed. `Template.Client.Tests/ArchitectureTests.cs` then asserts each project's reference list *exactly*. The map is the coarse net that fires the instant an illegal edge is written; the test pins the intended set. Two projects at the same rank — Client and Worker — may never reference each other; shared code moves **down** into Common, Business, or Data.

## Registration order

In `Template.Client/Program.cs` (and any other host), keep this order:

1. `AddServiceDefaults()`
2. `AddDataInfrastructure()`
3. `AddBusinessServices()`
4. Host-only services — rate limiting, MVC and auth in the Client; `AddContactDigest()` in the Worker

Keep host-only wiring in an extension method rather than inline in `Program.cs`. Top-level statements cannot be called from a test, so anything registered there — options binding and `ValidateOnStart` rules especially — is wiring nothing can assert over.

## Persistence

Default is Azure Tables/Blobs/Queues via Aspire (Azurite in Development). With `--data postgres` or `--data sqlserver`, AppHost wires that database and Data uses EF Core instead (`--data efcore` is an alias for postgres).

The `Testing` host environment skips real storage registration so `WebApplicationFactory` tests can inject fakes for store interfaces without Docker.

## Adding a feature

1. Put contracts/models in `Template.Common`.
2. Put use-case services in `Template.Business`; register them in `AddBusinessServices`.
3. Put stores/DbContext work in `Template.Data`; register them in `AddDataInfrastructure`.
4. Put HTTP surface in `Template.Client` (`Controllers/`, `Controllers/Api/` for `/api/v1/...`).
5. Prefer options with `BindConfiguration` + `ValidateDataAnnotations` + `ValidateOnStart`.

## Packages and build

- Bump NuGet versions only in `Directory.Packages.props` (central package management, including transitive pins via `CentralPackageTransitivePinningEnabled`).
- `Directory.Build.props` enables nullable, warnings-as-errors, editorconfig enforcement, `NuGetAudit` (vulnerable-package warnings), and holds the layer map. It also pins `InvariantGlobalization=false` — `ARCH0003` fails the build if that is ever flipped, because Microsoft.Data.SqlClient throws before it opens a socket in invariant mode and *every* database call fails in *every* environment with no build warning.
- SDK pin lives in `global.json`, and CI installs from it rather than from a floating version.
- Prefer LF line endings (see `.gitattributes` / `.editorconfig`).
- Run `pwsh ./eng/verify.ps1` before pushing — it's exactly what CI runs (restore, whitespace check, build, test, coverage floors, vulnerable-package scan) and fails on things `dotnet build` alone won't catch.
- Adding a project means four edits, each with its own failure if missed: the solution file, the layer map, `ArchitectureTests`, and the coverage floor table. See `CONTRIBUTING.md`.
- If you change `Template.Client`'s project references, update the `COPY` block in `Template.Client/Dockerfile` too — nothing but the `docker` CI job notices.

## Tests

| Project | Needs Docker | Covers |
|---------|--------------|--------|
| `Template.Business.Tests` | no | Domain services against fakes |
| `Template.Client.Tests` | no | HTTP boundary via `WebApplicationFactory`, architecture, the ARCH build guards, SEO endpoints |
| `Template.Data.Tests` | mostly | Both stores against a real backing service (Testcontainers), plus the in-memory store, which needs nothing |
| `Template.AppHost.Tests` | no | Aspire resource graph: every host references every backing resource |
<!--#if (UseWorker)-->
| `Template.Worker.Tests` | no | The digest loop (ticks, a failing tick, shutdown, one scope per tick) and `AddContactDigest`'s binding and fail-fast validation |
<!--#endif-->

`Template.AppHost.Tests` inspects the resource graph in process — it never calls `StartAsync`, so it starts no container. It exists because nothing else can see AppHost wiring drift: Business and Data tests supply their own storage, and Client tests run under the `Testing` environment, which skips real storage registration entirely.

## Why things are the way they are

Non-obvious decisions (why the architecture is layered this way, why EF Core starts on `EnsureCreated`) are recorded in `docs/adr/` — check there before reversing one.

## Security defaults already on the Client

Rate limiting, security headers, correlation ids, response compression, lowercase URLs, HSTS (non-dev), and `/health` + `/alive`. Abuse-prone anonymous POST paths go in `StrictPostPaths` inside `Template.Client/Hosting/RateLimitingExtensions.cs`.

When auth is Entra ID (`--auth azuread`), every controller requires a signed-in user unless marked `[AllowAnonymous]` (error/404/contact already are).

## What not to do

- Do not inject repositories/stores into controllers — go through Business services.
- Do not reference `Template.Data` from `Template.Business`.
- Do not put connection strings or secrets in source; use user secrets / environment / Aspire.
- Do not weaken CSP with `'unsafe-inline'` unless a view truly requires it — prefer external scripts/styles under `wwwroot`.
