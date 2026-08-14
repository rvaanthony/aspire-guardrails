# Layered Aspire Web Starter

[![build-and-test](https://github.com/rvaanthony/aspire-guardrails/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/rvaanthony/aspire-guardrails/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A reusable `dotnet new` starter for layered .NET Aspire web solutions with the base security and observability setup already wired.

## What's different about it

`dotnet new aspire-starter` gives you a working Aspire app. This gives you one whose structure holds up as it grows, because the rules are mechanical rather than documented:

- **The layering is a build error, not a convention.** A rank map makes an illegal project reference fail `dotnet build` with `ARCH0001`/`ARCH0002` — including the sideways reference between two hosts that "just shares a helper". Reviews miss that; the compiler does not.
- **The test suite has a floor that ratchets.** Per-assembly line coverage is gated, the merge across test projects is checked against its own arithmetic, and a new project cannot become exempt by accident.
- **The generated output is what gets tested.** CI creates six option combinations and runs each one's *own* verification gate. Three defects that were invisible in this repository — and would have hit real users on their first run — were caught exactly there.
- **One command is the whole gate.** `eng/verify.ps1` is what CI runs, verbatim, so "it passes locally" means something.

Everything below is opt-out: delete a workflow, drop a project, lower a floor. The point is that you start from enforced rather than aspirational.

## Installing

```powershell
dotnet new install <path-to-this-repo> --force
```

## Creating a new project

```powershell
dotnet new aspire-web -h                                          # parameter help
dotnet new aspire-web -n MyProject -o <path-to-new-project>

# Options (combinable):
dotnet new aspire-web -n MyProject -o <path> --auth azuread
dotnet new aspire-web -n MyProject -o <path> --logging sentry
dotnet new aspire-web -n MyProject -o <path> --data postgres
dotnet new aspire-web -n MyProject -o <path> --data sqlserver
dotnet new aspire-web -n MyProject -o <path> --worker --data postgres
dotnet new aspire-web -n MyProject -o <path> --auth azuread --logging sentry --data tables
```

| Parameter | Values | Default |
|-----------|--------|---------|
| `--auth` | `none`, `azuread` | `none` |
| `--logging` | `none`, `sentry` | `none` |
| `--data` | `tables`, `postgres`, `sqlserver` (`efcore` = alias for `postgres`) | `tables` |
| `--worker` | bool | `false` |
| `--skipRestore` | bool | `false` |

Every occurrence of `Template` is replaced with your project name, and fresh user-secrets ids are generated. The new project comes with its own `README.md`, `.github/` (CI, deploy stub, CodeQL, dependabot, PR template), `.devcontainer/`, and `CLAUDE.md` — separate from this repo's own (see `docs/adr/0003-stamp-staging.md`). Then:

```powershell
cd <path-to-new-project>
git init
dotnet run --project MyProject.AppHost   # Docker required (Azurite, Postgres, or SQL Server)
dotnet test MyProject.slnx               # Data.Tests need Docker; the rest do not
pwsh ./eng/verify.ps1                    # what CI runs: whitespace, build, test, coverage floors, vulnerable-package scan
```

The stamped project's `CONTRIBUTING.md` has a first-push checklist for the placeholders that ship in `.github/` (`@YOUR_GITHUB_USER` in CODEOWNERS, `YOUR_APP_NAME` in the deploy workflow, the tag ruleset the release process expects) and there is deliberately no `LICENSE` — that choice is the new project's.

## Solution layout

| Project | Responsibility |
|---------|----------------|
| `Template.AppHost` | Aspire orchestration (Azure Storage, Postgres, or SQL Server) |
| `Template.ServiceDefaults` | OpenTelemetry, service discovery, HTTP resilience, `/health` + `/alive` |
| `Template.Client` | MVC composition root + example `/contact` form + `/api/v1/contact` |
| `Template.Business` | Domain services (includes example `ContactIntakeService`) |
| `Template.Common` | Shared contracts |
| `Template.Data` | Store implementations (Tables or EF Core) |
| `Template.Worker` | Optional background service (`--worker`); refs ServiceDefaults/Business/Common/Data |
| `*.Tests` | xUnit. Client.Tests owns HTTP boundary + architecture tests; AppHost.Tests asserts the Aspire resource graph; Data.Tests use Testcontainers; Worker.Tests ship with `--worker` |

## Base setup included

- Rate limiting (strict bucket on `POST /contact`), security headers, correlation ids, JSON error handling, 404 re-execute
- Lowercase URLs, response compression, health probes
- Options pattern with `ValidateOnStart` where configured
- Vertical-slice example: Common → Data → Business → MVC + API
- Build hygiene: `net10.0`, warnings-as-errors, `.editorconfig`, Central Package Management with transitive pinning and `NuGetAudit`, `global.json` (CI installs the pinned SDK, not a floating version), Dependabot (NuGet, GitHub Actions, Docker, dev containers)
- Build-time architecture guardrails in `Directory.Build.targets`: a rank map makes an illegal project reference fail `dotnet build` (`ARCH0001`/`ARCH0002`) rather than waiting for review, and `ARCH0003` blocks `InvariantGlobalization=true`, which silently breaks every SQL Server call in every environment
- Single verification gate: `eng/verify.ps1` (restore, whitespace and line-ending check, build, test, per-assembly coverage floors, vulnerable-package scan) — this is exactly what CI runs
- Ratcheted coverage floors: `eng/coverage-floor.ps1` merges the per-project cobertura reports, gates each assembly against its own floor, and reports distance to an 80% target. `-Measure` reprints the live numbers
- CI: build/test the starter itself, build the client container image (which is the only thing that catches a stale `COPY` block in the Dockerfile), then stamp option combinations (including postgres, sqlserver, worker, and the efcore alias) and test them
- Deploy stub: `.github/workflows/deploy-azure.yml` (replace `YOUR_APP_NAME` + OIDC secrets) with a version-stamped publish and a retrying post-deploy health check
- Release workflows: CalVer RC branch cutting (`releases/rc-YYYY.MM.DD`, refuses to re-cut) and tag-and-publish (`vYYYY.MM.DD`, verifies CI passed on the exact commit, generates notes)
- Optional CodeQL workflow that skips itself while the stamped repo is private, so it turns itself on when the repo goes public instead of reddening CI for a missing GitHub Advanced Security licence
- `CODEOWNERS` for review routing (placeholder owner to replace on first push)
- `AGENTS.md` for agent/human conventions, `CONTRIBUTING.md` for the mechanics (prerequisites, adding a dependency or project, the LF line-endings recovery, releases), `CLAUDE.md` + `.claude/launch.json` for coding agents, `SECURITY.md`, and `docs/adr/` for the reasoning behind non-obvious decisions — all in stamped projects
- Dev Container (`.devcontainer/`) with .NET 10 and Docker-in-Docker

## Optional toggles

- **`--auth azuread`** — Microsoft.Identity.Web cookie sign-in, global authorize filter, login partial
- **`--logging sentry`** — `Sentry.AspNetCore`; empty `Sentry:Dsn` disables sending
- **`--data postgres`** / **`--data sqlserver`** — EF Core `AppDbContext` with that engine via Aspire (`--data efcore` is a postgres alias); starts on `EnsureCreated` and switches itself to `MigrateAsync` the moment you add a migration (`dotnet ef migrations add InitialCreate --project MyProject.Data --startup-project MyProject.Data`)
- **`--worker`** — adds `Template.Worker`, a background-service project wired into AppHost alongside the Client, with an example `PeriodicTimer`-based `BackgroundService`

## Conventions when growing a project

See `AGENTS.md`. Short version: register through `AddDataInfrastructure` / `AddBusinessServices`; bump packages only in `Directory.Packages.props`; keep controllers talking to Business, not stores.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) — it covers the two-source layout, how to verify a change against the option matrix, and the traps that stay green in this repository while breaking generated projects. [CLAUDE.md](CLAUDE.md) is the deeper reference for the templating mechanics.

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). Security issues go through [SECURITY.md](SECURITY.md), not a public issue.

## License

MIT — see [LICENSE](LICENSE). The license file is deliberately not copied into stamped output, so each project you create picks its own.
