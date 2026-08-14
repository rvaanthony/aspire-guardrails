# ADR 0004: EF Core growth path — EnsureCreated until you add a migration

## Status

Accepted

## Context

`EnsureCreatedAsync` and `MigrateAsync` are mutually exclusive schema-management strategies — `EnsureCreated` never writes to the `__EFMigrationsHistory` table, so mixing the two on the same database corrupts migration tracking. A freshly stamped app has zero migrations and no schema yet, so `EnsureCreated` is the right zero-friction default for `--data postgres`/`sqlserver`/`efcore`. But this starter can't know in advance whether a given app will ever add a real migration, and hard-coding `EnsureCreated` forever would silently strand every app on it, including ones that outgrow it.

## Decision

`DatabaseInitializerHostedService` checks `(await db.Database.GetPendingMigrationsAsync(...)).Any()` — more precisely, whether the context has any migrations at all (`Database.GetMigrations().Any()`) — at startup:

- If migrations exist, call `MigrateAsync` (applies pending migrations; safe on an already-migrated database).
- If none exist, fall back to `EnsureCreatedAsync` and log a warning containing the exact command to run: `dotnet ef migrations add InitialCreate`.

`dotnet-ef` ships as a local tool (`.config/dotnet-tools.json`) under the EF Core data backends, restored via `dotnet tool restore`. Migrations themselves are **not** generated or committed by this starter — a committed migration would immediately go stale against any model change, and a provider-specific one (Postgres vs. SQL Server) can't be made to compile cleanly across both `--data` branches.

## Consequences

- New apps get instant `EnsureCreated` schema creation with zero setup, same as before.
- The moment someone runs `dotnet ef migrations add`, the app automatically switches to `MigrateAsync` on next startup — no code change required, no flag to flip.
- The warning log is the only "documentation" pointing at this — anyone who never reads logs won't discover it until they need it, which is an acceptable tradeoff for a starter.
