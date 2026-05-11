# Contributing to Spydersoft Audit

This repo contains the audit platform service: the publish-side library
(`Spydersoft.Audit`), the typed query client (`Spydersoft.Audit.Client`), the
worker service (`Spydersoft.AuditProcessor`), the read API
(`Spydersoft.AuditApi`), an Aspire AppHost for local dev, a small seeder used
by the test fixture, and the committed OpenAPI snapshot + Kiota-generated
client code that depends on it.

## Initial Setup

```shell
# Restore .NET deps and the local Kiota tool.
dotnet restore src/Spydersoft.Audit.slnx
dotnet tool restore

# (Optional) Install Husky-managed git hooks.
yarn install
```

The Kiota dotnet tool is pinned in [.config/dotnet-tools.json](../.config/dotnet-tools.json).
You don't need a global Kiota install — the regen scripts use the local one.

## Solution Layout

```text
src/
  Spydersoft.Audit/                 publish-side library (NuGet)
  Spydersoft.Audit.UnitTests/
  Spydersoft.Audit.Client/          query-side client (NuGet) — wraps Kiota
    Generated/                      Kiota output, regenerated from snapshot
  Spydersoft.Audit.Client.UnitTests/
  Spydersoft.AuditProcessor/        worker (image: ghcr.io/spydersoft-consulting/audit-processor)
  Spydersoft.AuditProcessor.UnitTests/
  Spydersoft.AuditApi/              read API (image: ghcr.io/spydersoft-consulting/audit-api)
    openapi/audit-api-v1.json       committed OpenAPI snapshot
  Spydersoft.AuditApi.UnitTests/
  Spydersoft.AuditSeeder/           --token-only + Mongo seed for tests
  Spydersoft.Audit.AppHost/         Aspire host — RabbitMQ + Mongo + processor + api + seeder
tests/
  api-integration/                  Playwright API tests
docs/
scripts/
```

## Build, Lint, Format

```shell
dotnet build src/Spydersoft.Audit.slnx
dotnet format src/Spydersoft.Audit.slnx
```

Husky runs `lint-staged` pre-commit for formatting and `dotnet build` +
`dotnet test` pre-push.

## Tests

The repo has two test surfaces: .NET unit tests (NUnit) and Playwright API
tests against a live Aspire AppHost.

### .NET unit tests

Fast, in-memory, no infrastructure required:

```shell
dotnet test src/Spydersoft.Audit.slnx
```

Multi-targets run on net8.0 / net9.0 / net10.0 for the libraries; the
processor and API are net10.0-only. Current coverage: 75 tests across the
solution.

Mockability rules of thumb when authoring new tests:

- For the **publish side**, mock `IAuditEventEmitter` (recommended) or use
  Audit.NET's `UseNullProvider()` to disable audit entirely.
- For the **query side**, mock `IAuditQueryClient` directly. The
  Kiota-generated `AuditApiClient` is an implementation detail under
  `Spydersoft.Audit.Client.Internal.Generated` — don't depend on it from
  consumer code or tests.

### Playwright API tests

Live integration tests against the AuditApi running through the Aspire
AppHost. Requires:

- A container runtime accessible to Aspire (Docker Desktop, Podman, or Rancher
  Desktop with the `docker` CLI shim).
- Node 22+ and a recent Playwright (`npm install` in
  `tests/api-integration/` will pull `@playwright/test`).

Run them:

```shell
cd tests/api-integration
npm install
npx playwright test
```

What happens behind the scenes:

1. `playwright.config.ts` calls `dotnet run --project src/Spydersoft.AuditSeeder -- --token-only`
   to mint a JWT signed with the shared test key. The token is cached at
   `tests/api-integration/.auth/token.json` so reruns skip the dotnet boot.
2. The Playwright `webServer` block boots
   `src/Spydersoft.Audit.AppHost --launch-profile Testing`. Aspire spins up
   RabbitMQ + Mongo + audit-processor + audit-api + audit-seeder containers
   and waits for them to be healthy.
3. Tests seed via `POST /api/test/audits` (only registered in Testing
   environment via the [TestingOnlyControllerProvider](../src/Spydersoft.AuditApi/Infrastructure/TestingOnlyControllerProvider.cs)
   filter), assert via the public read endpoints, and clean up via
   `DELETE /api/test/audits` in `afterAll`.

The AppHost stays warm by default (`reuseExistingServer: !process.env.CI`) so
local reruns are fast.

## OpenAPI Snapshot + Kiota Client

The audit API exposes an OpenAPI document that's committed to the repo as a
snapshot. The Kiota-generated client in `src/Spydersoft.Audit.Client/Generated/`
is built from that snapshot. **Both files are committed** so consumer apps and
CI builds don't need Kiota installed and don't depend on the API being
reachable at build time.

### When to regenerate

| Change | Run |
| --- | --- |
| Edited a controller signature, added/removed an endpoint, changed a DTO shape | `pwsh ./scripts/regen-openapi.ps1` then `pwsh ./scripts/regen-client.ps1` |
| Bumped Kiota version in `.config/dotnet-tools.json` | `pwsh ./scripts/regen-client.ps1` |
| Edited only controller bodies, services, infra | Nothing — neither file changes |

Both scripts are idempotent. Diff the result; if it changed, commit it
alongside the API change so consumer apps see it through a normal NuGet bump.

### Regen workflow

```shell
# 1. Refresh the OpenAPI snapshot from the live AuditApi metadata.
pwsh ./scripts/regen-openapi.ps1

# 2. Regenerate the Kiota client from the snapshot.
pwsh ./scripts/regen-client.ps1

# 3. Update Spydersoft.Audit.Client/AuditQueryClient.cs mapping if the API shape
#    genuinely changed (new fields, renamed fields). Existing fields with new
#    nullability or formatting usually don't require wrapper changes.

# 4. Build + test.
dotnet test src/Spydersoft.Audit.slnx
```

### How the snapshot is exported

[`scripts/regen-openapi.ps1`](../scripts/regen-openapi.ps1) runs:

```shell
dotnet run --project src/Spydersoft.AuditApi -- --export-openapi <abs-path>
```

`--export-openapi` is a special mode handled at the top of the API's
`Program.cs`. It builds a stripped-down WebApplication (no Mongo, no auth, no
telemetry) just enough to serve `/openapi/v1.json`, fetches the document
in-process, and writes it to disk. See
[OpenApiExporter.cs](../src/Spydersoft.AuditApi/Infrastructure/OpenApiExporter.cs)
for details.

### Why the wrapper exists

`IAuditQueryClient` and `AuditQueryClient` exist to insulate consumer code
from Kiota churn:

- The generated `AuditApiClient` lives under
  `Spydersoft.Audit.Client.Internal.Generated` and changes shape on every
  regen.
- Public DTOs (`AuditQuery`, `AuditPage`, `AuditEventView`) are sealed
  records with stable shapes the wrapper translates to.
- Tests in consumer apps mock `IAuditQueryClient`, never the generated
  client.

Don't add `using Spydersoft.Audit.Client.Internal.Generated.*` outside
`AuditQueryClient.cs` and the test mapping fixtures.

## Versioning

GitVersion-driven SemVer (matches the platform repo's convention):

- `+semver: major` in PR title — major bump.
- `+semver: minor` in PR title — minor bump.
- Default — patch bump.

Both NuGets (`Spydersoft.Audit`, `Spydersoft.Audit.Client`) ship together at
the same version. The container images (`audit-processor`, `audit-api`) also
share that version.

Breaking-change rules:

- `AuditRecord` (publish wire) — additive-only within a major version.
- `AuditEventView` / `AuditPage` (query wire) — additive-only within a major
  version. A breaking change to the API response shape requires a coordinated
  major bump on `Spydersoft.AuditApi` + `Spydersoft.Audit.Client`.

## Identity Configuration

The `audit:read` scope and client grants are configured at runtime through
the IdentityServer admin UI for each environment, not in code. See
[identity-configuration.md](identity-configuration.md) for the per-env
runbook.

## Release Process

1. Merge to `main` — CI publishes feature-branch images and prerelease NuGets
   for testing.
2. Tag a release — CI publishes stable NuGets to GitHub Packages and tagged
   container images to `ghcr.io/spydersoft-consulting/*`.
3. Bump the image tag in `platform-helm-config/environments/test/images.yaml`
   to roll the deploy through test → stage → production.
