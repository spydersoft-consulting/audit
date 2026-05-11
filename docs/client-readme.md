# Spydersoft.Audit.Client

Typed HTTP client for the
[Spydersoft Audit query API](https://github.com/spydersoft-consulting/audit). Consume this
from any .NET app that wants to read audit history without hand-rolling HTTP calls.

## Usage

```csharp
services.AddSpydersoftAuditClient(configuration);

// elsewhere — inject IAuditQueryClient
var page = await client.SearchAsync(new AuditQuery
{
    Source = "pitstop",
    EntityType = "FillUp",
    Limit = 50,
});

var changeHistory = await client.GetByEntityAsync("FillUp", "42");
```

## Configuration

```json
{
  "AuditClient": {
    "BaseUrl": "https://audit.mattgerega.net",
    "TokenEndpoint": "https://auth.mattgerega.net/connect/token",
    "ClientId": "pitstop-api",
    "Scope": "audit:read"
  }
}
```

`ClientSecret` should come from user-secrets or a sealed-secret, never `appsettings.json`.

## Mockability

`IAuditQueryClient` is the only surface to depend on. Tests substitute a fake — no real
HTTP, no `WebApplicationFactory` over the audit API:

```csharp
var fake = Substitute.For<IAuditQueryClient>();
fake.GetByEntityAsync("FillUp", "42", Arg.Any<CancellationToken>())
    .Returns(new[] { new AuditEventView(/* ... */) });
```
