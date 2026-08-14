# CLAUDE.md

Guidance for Claude Code (or any coding agent) working in this repository.

## Commands

```powershell
dotnet run --project Template.AppHost   # run everything (Docker required: Azurite, Postgres, or SQL Server)
dotnet test Template.slnx               # all tests; Data.Tests need Docker, the rest do not
pwsh ./eng/verify.ps1                   # full gate: restore, whitespace check, build, test, coverage floors, vulnerable-package scan
pwsh ./eng/verify.ps1 -NoTest           # faster: restore, whitespace, build only
pwsh ./eng/coverage-floor.ps1 -Measure  # live per-assembly coverage, and distance to the target
```

`verify.ps1` is what CI runs — treat it as the source of truth for "does this pass," not just `dotnet build`.

## Guardrails that fail the build

- **`ARCH0001` / `ARCH0002`** — the layer map in `Directory.Build.props`, validated by `Directory.Build.targets`. A project missing from the map, or referencing one at the same or a higher rank, fails `dotnet build`. Shared code moves *down* a layer rather than sideways.
- **`ARCH0003`** — `InvariantGlobalization=true` is fatal to Microsoft.Data.SqlClient, silently and in every environment. Leave it false.
- **Coverage floors** — `eng/coverage-floor.ps1`, run by `verify.ps1`. A new product assembly needs a floor row, or the gate fails rather than exempting it.

Adding a project touches four places; [CONTRIBUTING.md](CONTRIBUTING.md) lists them with the failure each one produces if missed.

## Running the Client without Docker

```powershell
dotnet run --project Template.Client -- --environment Testing
```

That skips real storage registration in favor of an in-memory store, so you can iterate on the Client with nothing else running. The `client-no-docker` config in `.claude/launch.json` does the same thing.

**Pass the environment as an argument after `--`, not as an environment variable.** `ASPNETCORE_ENVIRONMENT=Testing dotnet run ...` does NOT work: `dotnet run` applies `Properties/launchSettings.json`, whose profile sets `ASPNETCORE_ENVIRONMENT=Development` and overrides the ambient variable. The app then tries to reach real Azure Storage and dies at startup with "A TableServiceClient could not be configured". Command-line arguments outrank launch-profile variables, which is why the form above works.

## Conventions

See [AGENTS.md](AGENTS.md) for the layering rules, registration order, and what not to do when adding a feature.
