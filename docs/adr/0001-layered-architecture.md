# ADR 0001: Layered architecture with an enforced dependency graph

## Status

Accepted

## Context

A starter solution grows fast once real features land. Without an enforced boundary, controllers end up talking directly to storage, domain logic leaks into the composition root, and the dependency graph turns into a ball of mud within a few sprints — the kind of thing that's expensive to unwind later and easy to prevent from day one.

## Decision

Split the solution into layers with a one-directional dependency graph:

- `Template.Common` — contracts, models; depends on nothing.
- `Template.Business` — domain services; depends only on `Common`.
- `Template.Data` — store/DbContext implementations of `Common` interfaces; depends on `Business`, `Common`.
- `Template.Client` — MVC composition root; depends on `Business`, `Common`, `Data`, `ServiceDefaults`.
- `Template.ServiceDefaults` — OpenTelemetry/health/resilience; depends on nothing project-local.
<!--#if (UseWorker)-->
- `Template.Worker` — background service; same allowed references as `Client`, and must never reference it.
<!--#endif-->
- `Template.AppHost` — Aspire orchestration only; the only project that may depend on a host.

Enforce it twice, at two different granularities.

**At build time**, a rank map in `Directory.Build.props` gives every project a number, and `ValidateLayering` in `Directory.Build.targets` fails the build when a project references one of equal or higher rank (`ARCH0002`) or when either end is missing from the map (`ARCH0001`). This fires the moment an illegal reference is written — in the IDE, before a test runs, before a review happens.

**At test time**, `Template.Client.Tests/ArchitectureTests.cs` reads each project's `<ProjectReference>` elements out of its `.csproj` and asserts they match an explicit allow-list exactly — no reflection, no runtime graph inspection, just the project file. `AllProjectsAreCoveredByArchitectureTest` then asserts every `*/*.csproj` appears in that list, so a new project added without an entry fails loudly instead of going unchecked.

The two are not redundant. The rank map permits *any* lower rank, so it catches illegal edges but not merely unintended ones; the test pins the precise set but only when the suite runs. The rank model also expresses something the allow-list cannot: two projects at the *same* rank may not reference each other, which is what stops one host depending on another to share a helper instead of moving that code down a layer.

`Template.Client.Tests/BuildGuardTests.cs` plants each violation and asserts the build rejects it, including a legal reference as a control — so the enforcement is itself tested rather than assumed.

## Consequences

- Adding a project means a row in the rank map *and* a row in `ArchitectureTests.CandidateDependencies`. Miss the first and the build fails with `ARCH0001`; miss the second and the test suite fails. Neither is a silent gap.
- Controllers must go through `Business` services, never `Data` stores or `DbContext` types, directly.
- `Business` must never reference `Data` (the reverse of the natural "business needs persistence" instinct) — persistence is a detail `Business` depends on through `Common`'s interfaces, not the other way around.
- A project that's optional per stamp (present only under certain `--data`/`--worker` choices) is handled by checking on-disk existence before asserting on it, so the test suite works unchanged across every stamped variant.
