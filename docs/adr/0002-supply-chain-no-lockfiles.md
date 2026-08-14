# ADR 0002: Supply-chain posture — pinning, not lockfiles

## Status

Accepted

## Context

NuGet lock files (`packages.lock.json`, generated via `RestorePackagesWithLockFile`/`--use-lock-file`) hash-pin an exact restore graph per project and are the strongest reproducibility guarantee NuGet offers. They're also generated per-project and embed that project's identity and restore graph as of generation time.

This repo is a `dotnet new` template: every project is renamed at stamp time (`sourceName` substitution rewrites `Template.*` to the new project's name throughout file and directory names), and `dotnet new` does not run a NuGet restore-with-lock-file step as part of instantiation. A lock file committed to this repo would therefore ship into every stamped app still referencing the *old* (pre-rename) project identity, and `--locked-mode` restores in CI would hard-fail on the very first restore because the graph no longer matches the renamed projects. A stale, mismatched lock file is worse than no lock file at all — it fails loudly on first use rather than degrading gracefully.

## Decision

Rely on Central Package Management for reproducibility instead:

- `Directory.Packages.props` pins every direct package version in one place (`ManagePackageVersionsCentrally`).
- `CentralPackageTransitivePinningEnabled` pins transitive package versions too, so nothing floats silently between restores.
- `NuGetAudit` + `NuGetAuditMode=all` scan direct and transitive packages for known vulnerabilities at restore/build time (as warnings, since a fresh CVE advisory shouldn't hard-break a build via `TreatWarningsAsErrors`).
- `eng/verify.ps1` runs `dotnet list package --vulnerable --include-transitive` as an explicit, controllable pass/fail gate — the actual enforcement point, independent of MSBuild warning behavior.

## Consequences

- Every direct and transitive version is explicit and pinned in one file, and upgrades are one-line diffs in `Directory.Packages.props`.
- This is *not* hash/integrity-locked the way `packages.lock.json` is — a compromised package version at the pinned version number could still be pulled if NuGet's own package integrity checks don't catch it. `NuGetAudit` covers known-CVE risk, not supply-chain tampering at a fixed version.
- If this template ever stops renaming projects on stamp (e.g. a future mode that copies files without `sourceName` substitution), revisit this decision — lock files would then be safe to adopt.
