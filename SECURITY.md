# Security

## Reporting a vulnerability

Report security issues privately rather than opening a public issue: use "Report a vulnerability" under this repository's Security tab to open a draft advisory, or contact the maintainer(s) directly. Private vulnerability reporting is free on public repositories once enabled in Settings → Security; on private repositories it requires GitHub Advanced Security.

## Baseline controls already in place

- Central Package Management with `NuGetAudit` and transitive pinning (`Directory.Packages.props`, `Directory.Build.props`) — known-vulnerable packages surface as build warnings and fail `eng/verify.ps1`'s vulnerable-package scan.
- Dependabot (`.github/dependabot.yml`) keeps NuGet, GitHub Actions, Docker base images, and dev container features current.
- Security headers, rate limiting, and correlation ids on every response (`Template.Client/Middleware`, `Template.Client/Hosting`).
- No secrets in source — connection strings and API keys come from user secrets, environment variables, or Aspire parameters. `.gitignore` excludes `.env*` (except `.env.example`) and `*.local.json`.
- Optional CodeQL static analysis (`.github/workflows/codeql.yml`) — requires GitHub Advanced Security if this repository is private.

## Not included

No automated secret scanning is configured (e.g. TruffleHog, Gitleaks) — this starter assumes a small or solo team; add one if that assumption stops holding. There's no NuGet dependency lockfile: Central Package Management with transitive pinning keeps every direct and transitive version explicit, and `eng/verify.ps1` is the enforcement gate for known-vulnerable packages. That covers known-CVE risk, not tampering with an already-pinned version.
