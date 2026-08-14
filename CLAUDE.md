# CLAUDE.md

Guidance for Claude Code (or any coding agent) working on this `dotnet new` template itself — not on an app stamped from it. Stamped apps get their own `CLAUDE.md` from `stamp/CLAUDE.md`; this file never ships (it's in the first source's exclude list).

## What this repo is

A `dotnet new` template (shortName `aspire-web`, sourceName `Template`) that stamps out layered .NET Aspire web solutions. Every file in this repo, outside the excludes below, is copied into every stamped app with the literal token `Template` replaced by the new project's name.

## Two audiences, two sets of files

- **Files that ship into stamped apps**: everything except `.template.config/`, `stamp/`, `.github/`, `.devcontainer/`, this `CLAUDE.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `RvaAnthony.AspireWeb.Templates.csproj`, `README.md`, `LICENSE`, `.claude/settings.local.json`, and any `docs/adr/*.md` explicitly listed in the exclude list — see `.template.config/template.json`. Note the asymmetry with `SECURITY.md`, which *does* ship and is written generically for that reason; `CODE_OF_CONDUCT.md` does not, because a stamped app should not inherit this repo's enforcement contact. An ADR belongs in that exclude list if it explains *this repo's own* templating mechanics (e.g. why `stamp/` exists) rather than something true of the stamped app's own architecture — that reasoning is meaningless once `stamp/`, `.template.config/`, and the option-matrix constraints no longer exist in the stamped output.
- **`stamp/`**: a *second* template source (`.template.config/template.json` → `sources[1]`), staged at the repo root and copied to the stamped app's root at instantiation time. It's how a stamped app gets a different `.github/`, `.devcontainer/`, `README.md`, and `CLAUDE.md` than this repo has for itself — the first source excludes those same paths so there's no collision. GitHub only executes workflows under a repo's own top-level `.github/workflows/`, so the YAML files under `stamp/.github/workflows/` are inert here; they only run once copied into a stamped repo. When changing what a stamped app's CI, devcontainer, or top-level docs look like, edit under `stamp/`, not the equivalent root path.

## Authoring rules for anything that ships (root files or stamp/)

- **Never write the bare word "template" in prose** (any casing) in a file that ships. Say "starter", "solution", or "this app" instead. `sourceName` substitution rewrites the literal token `Template` throughout shipped content, including inside prose, so the word reads oddly post-stamp and a future derived-form rule could rewrite it unexpectedly. `Template.Client`, `Template.slnx`, etc. are fine and intentional — those are meant to be substituted into the real project name.
- **Conditional code uses two comment styles, both read by the templating engine's conditional processor**:
  - C# (`#if (UseEfCore)` / `#elif` / `#else` / `#endif`) is also a real C# preprocessor directive. No `*.csproj` defines these symbols via `DefineConstants`, so they evaluate `false` in *this repo's own build* — the repo always builds/tests as the default configuration (`--auth none --logging none --data tables`), while every other branch is dead code from the compiler's perspective. The templating engine does the real inclusion/exclusion at stamp time based on the actual parameter values.
  - XML (`.csproj`, `.slnx`) uses `<!--#if (UseAzureAd)-->` / `<!--#elif-->` / `<!--#endif-->` as literal comments, so the block is inert in this repo's own build too.
- Whole-file inclusion (as opposed to in-file conditionals) is handled by `sources[0].modifiers[].condition` + `exclude` in `template.json` — used when a file only makes sense for one variant (e.g. `Template.Data/Persistence/**` only under EF Core).
- **Every MSBuild `Condition` in a shipped `.props`/`.targets`/`.csproj` must reference at least one `$(property)`.** The templating engine treats a `Condition` expression containing no `$(...)` as one of *its* conditions and rewrites the element accordingly — measured against SDK 10: an item-list test of the not-equals form has its `Condition` **silently deleted** (leaving a task that now always runs), and the equals form **deletes the whole element**. Item and metadata references (`@(...)`, `%(...)`) mean nothing to the engine, so a condition built only from them is the trap; adding a property reference is what marks the condition as MSBuild's. None of this reproduces in this repo, which builds these files exactly as written — it only appears in stamped output. `Directory.Build.targets` flattens item lists into properties before testing them for this reason. Two CI steps guard it: `Lint shipped MSBuild conditions` (source-side rule) and a `Condition=` count comparison in `Verify stamp output` (backstop for other triggers).
- **Do not write the literal text of an MSBuild `Condition` attribute inside a comment in a shipped file.** A comment that merely quoted the offending syntax caused the engine to delete the comment *and the following `PropertyGroup`*. Explain such hazards here instead, and leave a pointer in the shipped file.
- **Using-directive ORDER is not enforced, and cannot be.** Every project is named after the app, so whether `using <App>.Common;` belongs above or below `using Microsoft...;` depends on the app's name — it sorts after Microsoft for `Ravenna` and before it for `Acme`. No fixed order in the source is right for every name. `eng/verify.ps1` therefore runs `dotnet format whitespace --verify-no-changes`, which keeps whitespace and line-ending enforcement (verified: a stray double space still reports `WHITESPACE`, a CRLF still reports `ENDOFLINE`) and drops only the order rule. `dotnet format`'s `style` and `analyzers` passes are otherwise redundant here, because `EnforceCodeStyleInBuild` plus `TreatWarningsAsErrors` already fail the build on every IDE and CA diagnostic.

  This was found the hard way: a solution created as `Demo` failed its own format check on ten files, out of the box. It had been invisible because every name ever tested — `Smoke`, `Stamp`, `StampApp`, `Vauth`, `PackApp` — began with P, S or V and therefore sorted *after* `Microsoft`. **Roughly every name from A to L was broken.** The CI stamp matrix now straddles the boundary deliberately (`Acme` before, `Zenith` after) and must keep at least one of each.

  A related and now-defused version of the same thing: `dotnet format` does not reorder usings across a preprocessor directive, so a conditional `using` block whose branches resolve to different sort positions was also invisible here. `Template.Data/Extensions/DataHostingExtensions.cs` keeps the conditional above `Template.Data.Stores` for that reason. It is no longer gated, but it is still the correct shape.

- **Every port is generated per created project, never hardcoded.** `template.json` declares `generated`/`port` symbols whose `replaces` values are the literal ports in `Template.AppHost/Properties/launchSettings.json`, `Template.Client/Properties/launchSettings.json`, `.claude/launch.json`, and the `WithBlobPort`/`WithQueuePort`/`WithTablePort` calls in `AppHost.cs`. Before this, every generated app shared one set of ports: two of them could not run at once, and a leftover process blocked the next run with an opaque `address already in use`. Adding a new port means adding a symbol — a literal port in shipped content is a defect. The emulator ports additionally avoid Azurite's well-known 10000-10002, which any already-running Azurite holds; that collision surfaces as three `unable to allocate a network port` warnings followed by a **hang**, with nothing naming the cause. `Template.AppHost.Tests` asserts the emulator ports stay off the defaults, because the symptom appears on someone else's machine rather than in a failing test.

## Packaging

`RvaAnthony.AspireWeb.Templates.csproj` at the repo root packs the repository as `dotnet new` template content (`PackageType=Template`). It is excluded from `sources[0]` and from its own `Content` glob, so it never reaches a stamped app. It is deliberately **not** in `Template.slnx`, so `dotnet build`, `dotnet format` and `eng/verify.ps1` all ignore it — the `pack` CI job is the only thing that exercises packaging, and it does so by installing the produced package and stamping from it rather than by inspecting the archive.

Two settings there are load-bearing and look optional: `NoDefaultExcludes=true` (without it NuGet drops every dotted path, taking `.template.config/` with it — the package then installs cleanly and offers no templates) and `NoWarn` for `NU5110`/`NU5111` (NuGet flags `eng/*.ps1` as legacy install scripts, and `TreatWarningsAsErrors` promotes that to a build failure).

Publishing is `.github/workflows/publish-nuget.yml`, manual dispatch only, defaulting to a dry run, and it packs → installs → stamps → builds before it pushes. NuGet versions are immutable, so the irreversible step goes last.

## Verifying a change

```powershell
pwsh ./eng/verify.ps1                                         # format, build, test, coverage floors, vulnerable-package scan
dotnet new install . --force                                  # reinstall from local source
dotnet new aspire-web -n Smoke -o ../smoke
dotnet new aspire-web -n Smoke -o ../smoke-auth --auth azuread --logging sentry
dotnet new aspire-web -n Smoke -o ../smoke-pg --data postgres
dotnet new aspire-web -n Smoke -o ../smoke-sql --data sqlserver
```

Then, in each stamped output: confirm `stamp/` and `.template.config/` did **not** leak in; confirm `.github/workflows/`, `README.md`, `CONTRIBUTING.md`, and `eng/verify.ps1` **did**; grep for `Template\.` residue *and* for the bare word `template` in any casing (`sourceName` rewrites the lowercase form too — verified: `"template-client"` becomes `"acme-client"`, so a prose use of the word gets mangled); confirm no `#if`/`#endif` markers survived; confirm `Directory.Build.props` and `Directory.Build.targets` kept every `Condition=` they have in source; and run `dotnet test <Name>.slnx`.

There is no test filter any more. The old `--filter "FullyQualifiedName!~AppHost"` predated `Template.AppHost.Tests`, which inspects the Aspire resource graph in process without starting anything, so it needs no Docker and no exclusion.

## Conventions for the repo's own code

See [AGENTS.md](AGENTS.md) for the layering rules that apply both here and in stamped apps.
