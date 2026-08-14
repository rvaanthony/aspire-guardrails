# Contributing to Template

Layering rules, registration order, and the conventions to follow when adding a feature live in [AGENTS.md](AGENTS.md). Read that first; this file is the mechanics.

## Prerequisites

- **.NET 10 SDK** — the exact version is pinned in `global.json`, and CI installs it from there
- **Docker** — required for the full Aspire run and for `Template.Data.Tests`, which start real backing services with Testcontainers
- **PowerShell 7+** (`pwsh`) — `eng/verify.ps1` is the verification gate on every platform
<!--#if (UseEfCore)-->
- **`dotnet-ef`** — pinned in `.config/dotnet-tools.json`; run `dotnet tool restore` once after cloning
<!--#endif-->

## Local setup

```powershell
dotnet restore Template.slnx
dotnet build Template.slnx
dotnet run --project Template.AppHost      # needs Docker
```

Open the Aspire dashboard link the console prints to reach every service.

**Without Docker**, run the web app alone against an in-memory store:

```powershell
dotnet run --project Template.Client -- --environment Testing
```

`AddDataInfrastructure` short-circuits in the `Testing` environment, which is also what lets the HTTP boundary tests run without containers.

The environment goes **after `--`, as an argument**, and setting `ASPNETCORE_ENVIRONMENT` instead will not work. `dotnet run` applies `Properties/launchSettings.json`, whose profile sets that same variable to `Development` and wins over the ambient one; the app then reaches for real Azure Storage and fails at startup with "A TableServiceClient could not be configured". Command-line arguments outrank launch-profile variables.

Never commit secrets. Connection strings and API keys come from user secrets, environment variables, or Aspire parameters:

```powershell
dotnet user-secrets set "Some:Key" "value" --project Template.Client
```

## Line endings

This repository is LF-only: `.gitattributes` sets `* text=auto eol=lf` and `.editorconfig` sets `end_of_line = lf`. The `eol=lf` half is not redundant — `text=auto` normalizes to LF in the **index**, while `eol=lf` forces LF in the **working tree** as well, overriding a local `core.autocrlf=true`.

Without it, a Windows checkout gets CRLF files and `dotnet format Template.slnx whitespace --verify-no-changes` fails with `ENDOFLINE` errors on a clean tree, before you have written a line of code.

A fresh clone needs nothing. **A clone made before `eol=lf` was in place still has CRLF files**, because Git does not rewrite a working file whose content has not changed. Check with:

```bash
git ls-files --eol | grep -v w/lf
```

Anything listed needs the working tree refreshed once:

```bash
git rm --cached -r . && git reset --hard
```

**That discards uncommitted work — commit or stash first.**

## If the AppHost hangs on startup

Every port in this solution was generated when it was created, so it should not collide with anything else you run — including a second solution created from the same starter. Two failures are still worth recognising.

**Three `unable to allocate a network port` warnings, then nothing.** Something already holds a port the storage emulator wants. The emulator's host ports are set explicitly in `Template.AppHost/AppHost.cs`; change them there. This is a hard failure that only logs warnings, so the hang is the real symptom.

**`Failed to bind to address ... address already in use`.** Usually a previous run that did not exit. Find it with `netstat -ano | findstr <port>` and stop it; the ports themselves are in `Template.AppHost/Properties/launchSettings.json`.

## Adding a dependency

Central Package Management is on, so a `Version=` on a `PackageReference` is a build error.

1. Add `<PackageVersion Include="Foo" Version="1.2.3" />` to `Directory.Packages.props`.
2. Add `<PackageReference Include="Foo" />` to the project that uses it — the **lowest** layer that needs it.
3. Restore and confirm no `NU1605` downgrade and no audit failure. `NuGetAudit` reports known-vulnerable packages as warnings; `eng/verify.ps1` is what turns them into a failure.
4. If you added a **project** reference to `Template.Client`, add its `.csproj` to the `COPY` block in `Template.Client/Dockerfile`, or the container build breaks. The `docker` CI job is what catches this.

## Adding a project

Four places, and each one has a named failure if you miss it:

1. Create it and add it to `Template.slnx`.
2. Add a `<TemplateLayer Include="…" Rank="NN" />` row to the layer map in `Directory.Build.props`. Without it the build fails with **`ARCH0001`**. A project may only reference projects of a **strictly lower** rank; violations fail with **`ARCH0002`**.
3. Add it to `CandidateDependencies` in `Template.Client.Tests/ArchitectureTests.cs`, which asserts each project's reference list exactly. `AllProjectsAreCoveredByArchitectureTest` fails if you don't.
4. If it is a product assembly that shows up in coverage, add a floor row to `eng/coverage-floor.ps1`. The gate fails on an assembly it has no floor for, so a new project cannot become permanently exempt by accident.

Do not set `TargetFramework`, `Nullable`, `ImplicitUsings`, or `LangVersion` in the `.csproj` — they come from `Directory.Build.props` so the projects cannot drift apart.

If two hosts need the same code, move it **down** into `Template.Common`, `Template.Business`, or `Template.Data` rather than referencing sideways. Two projects at the same rank referencing each other is exactly what `ARCH0002` exists to stop.

## Before submitting

```powershell
pwsh ./eng/verify.ps1
```

That is what CI runs, in order: restore, a whitespace and line-ending check, build (Release, warnings are errors), test, per-assembly coverage floors, and a vulnerable-package scan.

Using-directive order is deliberately not gated. Whether `using Template.Common;` sorts above or below `using Microsoft...;` depends on this app's name, so no ordering could be correct for every name the starter generates. Your IDE and a manual `dotnet format` will still sort them; CI just will not fail you for it. Use `-NoTest` for a faster format-and-build pass while iterating.

Two things it does not cover:

- **The container build**, if you changed `Template.Client`'s project references: `docker build -f Template.Client/Dockerfile -t client:dev .`
- **Coverage headroom.** The gate only enforces the floors. `pwsh ./eng/coverage-floor.ps1 -Measure` prints the live numbers and, for each assembly, how many more covered lines would reach the 80% target. Raising a floor after adding tests is a normal part of a PR; lowering one needs a reason in the diff.

## Coverage floors

`eng/coverage-floor.ps1` merges the cobertura report every test project writes and gates each product assembly against its own floor. The merge matters: each report only covers what that test assembly happened to load, so a shared layer appears several times at several different rates, none of them its real coverage.

The floors are a ratchet, not a target. They sit below the measured values on purpose, so that a refactor which adds a line before it adds a test does not redden the build — the cliff that gets a gate switched off permanently.

**The floors that shipped were measured against the example contact slice, and deleting that slice is the expected first thing you will do.** When you do, expect this gate to fail: it is measuring the absence of the example rather than a regression. Re-derive the numbers once with

```powershell
pwsh ./eng/coverage-floor.ps1 -Measure
```

and edit the table at the top of that file. There is deliberately no switch that rewrites the floors for you — a gate that can lower itself on command is one command away from meaning nothing, so moving a floor stays an edit somebody can see in a diff.

<!--#if (UseEfCore)-->
## Database migrations

The app starts on `EnsureCreated`, which creates the schema with no migration at all. `DatabaseInitializerHostedService` switches to applying migrations automatically as soon as one exists, with no code change:

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate --project Template.Data --startup-project Template.Data
```

`AppDbContextFactory` is what lets `Template.Data` be its own startup project, so `dotnet ef` needs no running host.

Review the generated migration before committing, and never edit one that has already been applied. Generated migrations are excluded from the coverage denominator by `coverage.runsettings`, so adding them does not move the floors.

See [docs/adr/0004-ef-migration-growth-path.md](docs/adr/0004-ef-migration-growth-path.md) for the reasoning.

<!--#endif-->
## First-push checklist

A few files ship with placeholders that do nothing useful until you replace them:

- `.github/CODEOWNERS` — replace `@YOUR_GITHUB_USER`. Until then GitHub reports every line as an unknown owner and requests no reviewers.
- `.github/workflows/deploy-azure.yml` — replace `YOUR_APP_NAME` in three places and set the `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` secrets. See [README.md](README.md).
- `.github/workflows/codeql.yml` — skips itself while the repository is private, because code scanning needs GitHub Advanced Security there. It starts running by itself when the repository goes public. Delete it if you don't want CodeQL.
- **Add a ruleset over `refs/tags/v*` blocking deletions and updates** before you publish a release. Without it the tag the release process treats as immutable can be moved, which defeats the point of tagging rather than branching.
- Add a `LICENSE`. One is deliberately not shipped, because the choice is yours.

## Pull requests

- Keep changes focused. Separate mechanical moves from behavior changes.
- Every change lands through a PR with green CI. Protect `main` and prefer squash merges, so each PR becomes one commit on the trunk.

## Branching and releases

Trunk-based. `main` is always releasable; work happens on short-lived branches off it. Release candidates are cut as freeze points, not long-lived lines of development.

| Ref | Shape | Purpose |
|---|---|---|
| `main` | protected trunk | Always releasable. Squash-merge target. |
| `feature/*`, `bug/*`, `chore/*` | short-lived | Merged and deleted. |
| `releases/rc-YYYY.MM.DD` | freeze point | Cut from `main`. Only deliberate fixes land on it. |
| `vYYYY.MM.DD` | immutable tag | What actually shipped. |

Dates are CalVer so refs sort chronologically. A second release on the same day gets a numeric suffix: `v2026.08.13.1`.

**Cutting and shipping a release**

1. Run **Release - Cut RC branch**. It creates `releases/rc-<today>` from `main` and refuses to overwrite an existing one.
2. Deploy that branch to a staging environment and verify it.
3. Run **Release - Publish** with the branch selector pointed at the RC branch. It checks that CI passed on the exact commit, creates the `v<date>` tag, and publishes a GitHub Release whose notes are generated from the PRs merged since the last one.
4. Deploy the tag to production.

The tag is the artifact of record. Because tags are immutable, "what is in production?" always has one exact answer, and any two releases can be diffed directly on GitHub.
