# Spydersoft Audit

![GitHub License](https://img.shields.io/github/license/spydersoft-consulting/audit)

A platform service that aggregates audit records from any Spydersoft application into a
single queryable store. Apps publish audit events via the `Spydersoft.Audit` NuGet package;
the `AuditProcessor` worker writes them to MongoDB; the `AuditApi` exposes a read-only HTTP
surface for browsing the log.

This repo replaces the previous app-scoped audit implementations (e.g. PitStop's homebrew
EF interceptor) with a shared pipeline, so every application's audit trail lives in one
place.

## Components

| Project | Role |
| --- | --- |
| `Spydersoft.Audit` | NuGet — publish side. `IAuditEventEmitter`, Audit.NET data provider, DI helpers, `AuditRecord` wire contract. |
| `Spydersoft.Audit.Client` | NuGet — query side. Typed `IAuditQueryClient` for apps and UIs that read audit history. |
| `Spydersoft.AuditProcessor` | Worker service — subscribes to the `audit.events` RabbitMQ topic and persists to MongoDB. |
| `Spydersoft.AuditApi` | HTTP read API over MongoDB. Authenticated via JWT, single `audit:read` scope. |
| `Spydersoft.Audit.AppHost` | .NET Aspire host for local development of this repo (RabbitMQ + Mongo containers + processor + api). |

## Repository Layout

```text
audit/
  src/
    Spydersoft.Audit/                  ← library (publish)
    Spydersoft.Audit.UnitTests/
    Spydersoft.Audit.Client/           ← library (query)
    Spydersoft.Audit.Client.UnitTests/
    Spydersoft.AuditProcessor/         ← worker
    Spydersoft.AuditProcessor.UnitTests/
    Spydersoft.AuditApi/               ← API
    Spydersoft.AuditApi.UnitTests/
    Spydersoft.Audit.AppHost/          ← Aspire host
    Spydersoft.Audit.slnx
    Directory.Build.props
    Directory.Packages.props
    nuget.config
  docs/
    identity-configuration.md          ← per-env IdentityServer admin-UI runbook
  Dockerfile.processor
  Dockerfile.api
  GitVersion.yml
  package.json
  README.md
  LICENSE
```

## Getting Started (Local Dev)

```bash
# Restore + build everything
dotnet build src/Spydersoft.Audit.slnx

# Run the full pipeline locally — RabbitMQ + Mongo + processor + API in containers
dotnet run --project src/Spydersoft.Audit.AppHost
```

## Consuming the NuGet Packages

For application integration (publishing audit events), see
[the consumer-integration plan](https://github.com/spydersoft-consulting/audit/blob/main/docs/consumer-integration.md)
or the broader plan in the [pitstop plans repo](../plans/audit/).

## Identity Configuration

The `audit:read` scope and client grants are configured at runtime via the IdentityServer
admin UI — **not** through code in `identity_server`. Each environment is configured
independently. See [docs/identity-configuration.md](./docs/identity-configuration.md) for
the per-env runbook.

## Contribution

See [docs/CONTRIBUTING.md](./docs/CONTRIBUTING.md) for setup, the test
strategy (unit + Playwright), the OpenAPI/Kiota regen workflow, and
versioning rules. Lint/format conventions inherit from the
[platform repo](https://github.com/spydersoft-consulting/platform/blob/main/docs/CONTRIBUTING.md).
