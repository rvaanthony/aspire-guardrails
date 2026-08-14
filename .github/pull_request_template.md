## What

<!-- What does this change do? -->

## Why

<!-- Why is this change needed? -->

## Verification

- [ ] `pwsh ./eng/verify.ps1` passes locally
- [ ] Stamped locally (`dotnet new install . --force && dotnet new aspire-web -n Smoke -o ../smoke`) and built at least one variant
- [ ] `git diff` reviewed for accidental sourceName residue or stray absolute paths

## Risk & rollback

<!-- Blast radius if this is wrong, and how to roll it back. -->
