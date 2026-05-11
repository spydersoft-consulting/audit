# Spydersoft.Audit

Audit.NET data provider that publishes structured audit records via
[Spydersoft.Messaging](https://github.com/spydersoft-consulting/platform). Consumed by
applications that want to emit audit events to the shared Spydersoft audit pipeline.

## Overview

This package owns:

| Type | Purpose |
| --- | --- |
| `AuditRecord` | Wire contract published to RabbitMQ (or any future transport). |
| `AuditOptions` | `Source` + `Topic` configuration. |
| `IAuditEventEmitter` | Mockable surface — substitute in tests. |
| `MessagingAuditEventEmitter` | Default emitter that wraps `IMessagePublisher`. |
| `MessagingAuditDataProvider` | Audit.NET `AuditDataProvider` that maps `AuditEvent`s and emits them. |

Storage is the `Spydersoft.AuditProcessor` worker's responsibility — this library does
not depend on MongoDB or any storage layer.

## Configuration

```json
{
  "Audit": {
    "Source": "pitstop",
    "Topic": "audit.events"
  }
}
```

`Source` is required and identifies the publishing application on every emitted record.

## Registration

```csharp
services
    .AddSpydersoftMessaging()
    .AddSpydersoftRabbitMq(configuration)
    .AddSpydersoftAudit(configuration);

var app = builder.Build();

// Wire Audit.NET's static config to the DI-registered provider.
var provider = app.Services.GetRequiredService<MessagingAuditDataProvider>();
Audit.Core.Configuration.Setup().UseCustomProvider(provider);
```

For EF Core consumers, also opt the `DbContext` into Audit.NET in `OnConfiguring`:

```csharp
optionsBuilder.UseAudit();   // from Audit.EntityFramework
```

## Mockability

Three levels of mocking, in order of recommendation:

| Level | Mock | When |
| --- | --- | --- |
| Domain | `IAuditEventEmitter` | "An audit record for X with action Y was emitted." |
| Disabled | `Audit.Core.Configuration.Setup().UseNullProvider()` | The test doesn't care about audit at all. |
| Transport | `IMessagePublisher` | The test asserts the topic + envelope. Rare. |

The expected default for new tests is the **domain level** — substitute
`IAuditEventEmitter` with a recording fake.

## Failure Semantics

- **Emission failures are swallowed.** `MessagingAuditEventEmitter` logs at warning level
  and continues — audit must never break the user-facing operation.
- The data provider does *not* fan out exceptions from the emitter back to
  `SaveChangesAsync`/the host call.
