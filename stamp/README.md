# Template

A layered .NET Aspire web solution.

## Run it

```powershell
dotnet run --project Template.AppHost   # Docker required (Azurite, Postgres, or SQL Server)
```

Open the Aspire dashboard link printed in the console to see the running services.

## Test it

```powershell
dotnet test Template.slnx               # Data.Tests need Docker; Client/Business tests do not
pwsh ./eng/verify.ps1                   # full gate: restore, whitespace, build, test, coverage floors, vulnerable-package scan
```

`pwsh ./eng/verify.ps1 -NoTest` skips the test step for a faster whitespace + build check.

<!--#if (UseEfCore)-->
## Database migrations

New apps start on `EnsureCreated` (zero-config schema creation). Once you add a migration, `DatabaseInitializerHostedService` automatically switches to applying migrations on startup instead — no code change needed. To add the first one:

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate --project Template.Data --startup-project Template.Data
```

See [docs/adr/0004-ef-migration-growth-path.md](docs/adr/0004-ef-migration-growth-path.md) for the reasoning.

<!--#endif-->
## Solution layout

| Project | Responsibility |
|---------|----------------|
| `Template.AppHost` | Aspire orchestration (Azure Storage, Postgres, or SQL Server) |
| `Template.ServiceDefaults` | OpenTelemetry, service discovery, HTTP resilience, `/health` + `/alive` |
| `Template.Client` | MVC composition root + example `/contact` form + `/api/v1/contact` |
| `Template.Business` | Domain services |
| `Template.Common` | Shared contracts |
| `Template.Data` | Store implementations (Tables or EF Core) |
<!--#if (UseWorker)-->
| `Template.Worker` | Background service (refs ServiceDefaults/Business/Common/Data) |
<!--#endif-->
| `*.Tests` | xUnit; architecture tests enforce layering; Data.Tests use Testcontainers |

See [AGENTS.md](AGENTS.md) for the layering rules, registration order, and conventions to follow when adding a feature, and [CONTRIBUTING.md](CONTRIBUTING.md) for the mechanics — prerequisites, adding a dependency or a project, the verification gate, and the release process. CONTRIBUTING.md also has a first-push checklist for the placeholders that ship in `.github/`.

## Deploying

`.github/workflows/deploy-azure.yml` is a starter workflow for Azure App Service over OIDC. Before it will run:

1. Create an App Service and an Entra app registration for GitHub OIDC (federated credential subject: `repo:<owner>/<repo>:environment:production`).
2. Set repository secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.
3. Replace `YOUR_APP_NAME` in the workflow with your App Service name (three places: `environment.url`, the deploy step, and the health-check URL).
4. Push to the `env/production` branch, or run the workflow manually.

## CI

`.github/workflows/build-and-test.yml` runs `pwsh ./eng/verify.ps1` on every push/PR. `.github/workflows/codeql.yml` runs static analysis (requires GitHub Advanced Security if this repository is private — see the comment at the top of that file; delete it if you don't want CodeQL). Dependabot is configured in `.github/dependabot.yml`.
