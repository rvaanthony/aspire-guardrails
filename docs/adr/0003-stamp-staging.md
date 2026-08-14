# ADR 0003: A second template source for stamp-only files

## Status

Accepted

## Context

A stamped app and this template repo need genuinely different top-level files, not just find-and-replaced versions of the same ones:

- This repo's CI stamps five option-matrix variants and builds/tests each one; a stamped app's CI just builds and tests itself once. Shipping this repo's own workflow into every stamped app would have it try to run `dotnet new aspire-web` recursively, which doesn't work outside this repo and makes no sense as a starting point for a real app.
- This repo has no README worth shipping (it documents *this template*, e.g. `dotnet new install`); a stamped app needs a README about *itself* (running it, testing it, deploying it).
- The dev container for working on the template installs the template into the local SDK on create; a stamped app's dev container should not re-run that.

Excluding these paths from the default source and hand-writing a from-scratch stamped version inline isn't practical when there are this many of them (workflows, dependabot config, PR template, devcontainer, README, CLAUDE.md).

## Decision

`.template.config/template.json` declares a second entry in `sources`: `{ "source": "./stamp/", "target": "./" }`. Its contents land at the stamped output's root exactly like the default source's do — same `sourceName` substitution, same text processing. The default (first) source excludes `.github/**`, `.devcontainer/**`, `stamp/**`, and `CLAUDE.md`, so there's no collision between what each source provides.

GitHub only executes workflows under a repository's own top-level `.github/workflows/` — so the YAML under `stamp/.github/workflows/` is completely inert sitting in *this* repo. It only starts running once `dotnet new` has copied it into a stamped repo's root.

## Consequences

- Anything under `stamp/` (or any other file that ships) must avoid the bare word "template" in prose — see `CLAUDE.md` for the full rule — since `sourceName` substitution rewrites it throughout shipped content.
- The template repo's own CI must assert that `stamp/` and `.template.config/` do **not** appear in stamped output, and that the expected staged files **do** — a mistake in either exclude list would otherwise ship silently wrong output. See the "Verify stamp output" step in `.github/workflows/build-and-test.yml`.
- Changing what a stamped app's CI, devcontainer, or top-level docs look like means editing under `stamp/`, not the equivalent root-level path (which governs this repo's own tooling instead).
