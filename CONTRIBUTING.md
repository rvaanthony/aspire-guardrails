# Contributing

Thanks for taking a look. This file is about working on **the template itself**. If you generated a project from it and want to contribute to *that*, the generated repository has its own `CONTRIBUTING.md`.

[CLAUDE.md](CLAUDE.md) is the deeper reference for the templating mechanics — it is written for coding agents, but everything in it applies to humans equally, and it records the traps in more detail than this file does. Read it before your first change to anything under `.template.config/` or `stamp/`.

## Prerequisites

- **.NET 10 SDK** — the exact version is pinned in [global.json](global.json)
- **PowerShell 7+** (`pwsh`) — [eng/verify.ps1](eng/verify.ps1) is the verification gate on every platform
- **Docker** — `Template.Data.Tests` start real backing services with Testcontainers, and the container build needs it

## The one thing to understand first

Every file in this repository, outside a short exclude list, is copied into every generated project with the literal token `Template` replaced by the new project's name. That has two consequences that catch everyone once:

1. **This repository is also a working solution**, built and tested as the default configuration (`--auth none --logging none --data tables`). Every other branch of every conditional is dead code from the compiler's point of view. So a change that builds here has only been proven for one of the option combinations.
2. **Substitution applies to prose, not just identifiers**, and it rewrites the lowercase form too — `"template-client"` becomes `"myapp-client"`. So a shipped file must never use the bare word "template" in a sentence. Say "starter", "solution", or "this app".

There are two template sources: the repository root, and [stamp/](stamp), which is staged over the root at generation time. That is how a generated project gets a different `.github/`, `.devcontainer/`, `README.md`, `CONTRIBUTING.md`, and `CLAUDE.md` than this repository has for itself. **When changing what a generated project's CI, dev container, or top-level docs look like, edit under `stamp/`, not the equivalent root path.**

## Traps that stay green here and break generated projects

These are all real, all found by verifying generated output rather than by review, and all invisible to `dotnet build` in this repository. [CLAUDE.md](CLAUDE.md) has the measurements.

- **An MSBuild `Condition` containing no `$(property)` reference** is evaluated by the templating engine as one of *its* conditions. An item-list test loses its condition entirely; the equals form deletes the whole element. Every condition in a shipped `.props`/`.targets`/`.csproj` must reference a property. CI lints for this.
- **Quoting that syntax inside an XML comment** triggers the same processing and took out the comment plus the element after it. Explain such hazards in `CLAUDE.md` instead.
- **Using-directive order is not enforced, and cannot be.** Project names become namespace prefixes, so whether `using <App>.Common;` sorts above or below `using Microsoft...;` depends on the app's name. `eng/verify.ps1` runs `dotnet format whitespace`, which keeps whitespace and line-ending checks and drops only the order rule. A solution created as `Demo` once failed its own format check on ten files because every name previously tested began with P, S or V and sorted after `Microsoft`; the CI matrix now straddles that boundary on purpose.
- **A double hyphen cannot appear inside an XML comment.** The file fails to parse and every project then reports `NU1015`, as though Central Package Management had broken.

## Verifying a change

```powershell
pwsh ./eng/verify.ps1
```

Restore, whitespace/line-ending check, build (Release, warnings are errors), test, per-assembly coverage floors, vulnerable-package scan. This is exactly what CI runs for the repository itself.

Then verify the **generated output**, which is where most defects actually live:

```powershell
dotnet new install . --force
dotnet new aspire-web -n Smoke -o ../smoke
dotnet new aspire-web -n Smoke -o ../smoke-auth --auth azuread --logging sentry
dotnet new aspire-web -n Smoke -o ../smoke-pg   --data postgres
dotnet new aspire-web -n Smoke -o ../smoke-sql  --data sqlserver
dotnet new aspire-web -n Smoke -o ../smoke-wk   --worker --data postgres
```

In each generated project, confirm that `stamp/` and `.template.config/` did **not** come along, that `.github/workflows/`, `README.md`, `CONTRIBUTING.md` and `eng/` **did**, that no `Template.` or bare `template` residue survived, that no `#if`/`#endif` markers are left, and then run its own gate:

```powershell
pwsh ./eng/verify.ps1
```

Running the generated project's gate rather than a bare `dotnet test` is deliberate: the whitespace check, the coverage floors and the vulnerable-package scan have each caught a defect that exists only in generated output. Two of the traps above were found exactly this way.

If you touched packaging, also check that the package works by using it, not by reading it:

```powershell
dotnet pack RvaAnthony.AspireWeb.Templates.csproj -c Release -o ./artifacts
dotnet new install ./artifacts/RvaAnthony.AspireWeb.Templates.1.0.0.nupkg
```

CI does all of the above across six option combinations, so a PR does not have to — but a change to `template.json`, `stamp/`, or the packaging project is worth checking locally first, because those failures are the slowest to diagnose from a log.

## Adding an option

Options live in `symbols` in [.template.config/template.json](.template.config/template.json), usually as a `parameter` plus a `computed` symbol that the conditionals actually test. Then:

- **In-file conditionals** for code that differs by option: `#if (UseThing)` in C# (a real preprocessor directive, so it must compile in the default configuration too), `<!--#if (UseThing)-->` in XML.
- **Whole-file inclusion** via `sources[0].modifiers[].condition` + `exclude`, when a file only makes sense for one variant.
- **Add the combination to the CI matrix** in [.github/workflows/build-and-test.yml](.github/workflows/build-and-test.yml). An option nothing generates is an option nothing tests.

## Windows line endings

The repository is LF-only (`.gitattributes` sets `* text=auto eol=lf`). A clone made before that was in place still has CRLF in the working tree, because Git does not rewrite a file whose content has not changed — and `dotnet format --verify-no-changes` then fails on files you have never touched. Check with `git ls-files --eol | grep -v w/lf`; if anything is listed, refresh the tree once with `git rm --cached -r . && git reset --hard`, which **discards uncommitted work**.

## Pull requests

- Keep changes focused, and separate mechanical moves from behaviour changes.
- Say which option combinations you verified against. "Builds here" is not the same as "works generated".
- New guardrails should come with evidence that they fire. A gate nobody has seen fail is decoration — the existing ones were each tested by planting the failure they claim to catch.
- Non-obvious decisions belong in `docs/adr/`. If the reasoning is about *this repository's templating mechanics* rather than the generated app's architecture, add the ADR to the exclude list in `template.json` so it does not ship into projects where it means nothing.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).
