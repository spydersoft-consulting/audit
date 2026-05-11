# IdentityServer Configuration — Per-Env Runbook

The audit pipeline depends on an OAuth2 scope (`audit:read`) and a small set of
clients/resources defined in IdentityServer. **None of this is in code** — scope and
client management is configured at runtime through the IdentityServer admin UI. Each
environment (test, stage, production) is configured independently.

This document is the runbook for that configuration. Follow it once per environment.

---

## Per-Environment Admin URLs

| Env | Admin UI | Discovery endpoint |
| --- | --- | --- |
| test | `https://auth.mattgerega.net` | `https://auth.mattgerega.net/.well-known/openid-configuration` |
| stage | `https://auth.mattgerega.org` | `https://auth.mattgerega.org/.well-known/openid-configuration` |
| production | `https://auth.mattgerega.com` | `https://auth.mattgerega.com/.well-known/openid-configuration` |

Sign in with an admin-level credential. The shape of the admin UI may shift across
IdentityServer versions; the field names below are the conceptual names — match them to
whatever the current UI calls them.

---

## What to Configure

### 1. API resource: `audit-api`

| Field | Value |
| --- | --- |
| Name | `audit-api` |
| Display Name | `Spydersoft Audit API` |
| Description | `HTTP read API over the Spydersoft audit log` |
| Enabled | yes |
| User Claims | (none for v1 — the API doesn't pull additional claims off the token) |

### 2. API scope: `audit:read`

Defined on the `audit-api` resource above (or as a standalone scope and associated with
the resource — depends on IdentityServer's admin model).

| Field | Value |
| --- | --- |
| Name | `audit:read` |
| Display Name | `Read audit history` |
| Description | `Allows reading the audit log via the audit API` |
| Required | no |
| Emphasize | no |
| Show in Discovery | yes |

### 3. Clients granted `audit:read`

Phase 1 grants the scope to **only the clients that actually call the API**. PitStop in
Phase 1 only **publishes** audit events (no reads), so it does not need the grant yet —
add it later when "show change history" UI is built.

| Client ID | Type | Allowed scope | Why |
| --- | --- | --- | --- |
| `audit-viewer` | Interactive (auth code + PKCE) | `audit:read` (+ `openid`, `profile`) | For the future audit UI. Stub this client now or add when the UI exists. |

When PitStop (or any consuming service) adds an "audit history" feature, register a new
machine client at that time:

| Client ID | Type | Allowed scope |
| --- | --- | --- |
| `pitstop-api` | Machine (client_credentials) | `audit:read` |

---

## Per-Field Cheat Sheet

When creating the interactive `audit-viewer` client, the typical fields are:

| Field | Value |
| --- | --- |
| Client ID | `audit-viewer` |
| Client Name | `Spydersoft Audit Viewer` |
| Allowed Grant Types | Authorization Code |
| Require PKCE | yes |
| Require Client Secret | no (public SPA-style client) |
| Allowed Scopes | `openid profile audit:read` |
| Redirect URIs | placeholder until UI exists; e.g. `https://audit.<env-hostname>/signin-oidc` |
| Post-Logout Redirect URIs | `https://audit.<env-hostname>/signout-callback-oidc` |
| Allowed CORS Origins | `https://audit.<env-hostname>` |

For a future machine client (e.g. `pitstop-api`):

| Field | Value |
| --- | --- |
| Client ID | `pitstop-api` |
| Client Name | `PitStop API` |
| Allowed Grant Types | Client Credentials |
| Require Client Secret | yes |
| Allowed Scopes | `audit:read` |
| Token Lifetime | default (3600 s) |

The client secret goes into Vault (or whatever secret store the consumer uses), never the
admin UI display.

---

## Verify

After saving:

```bash
curl -s https://auth.mattgerega.net/.well-known/openid-configuration \
  | jq '.scopes_supported'
```

You should see `"audit:read"` in the array.

For a smoke test of the machine client (when configured), run a client_credentials grant:

```bash
curl -s -X POST https://auth.mattgerega.net/connect/token \
  -d grant_type=client_credentials \
  -d client_id=pitstop-api \
  -d client_secret=<from-vault> \
  -d scope=audit:read \
  | jq .
```

Expect a JWT in `access_token`. Decode it at <https://jwt.io> and confirm:

- `iss` matches the env's authority URL
- `aud` includes `audit-api`
- `scope` (or `scp`) contains `audit:read`

Hit the API:

```bash
curl -s https://audit.mattgerega.net/api/audits?limit=1 \
  -H "Authorization: Bearer $TOKEN" \
  | jq .
```

Expect `200 OK` with a `{ items: [...], total: ..., skip: 0, limit: 1 }` shape (empty
items is fine before the first audit record is published).

---

## Rollback

To remove the configuration (e.g. accidentally created in the wrong env):

1. Disable the `audit-api` resource (don't delete — disable is reversible).
2. Disable any clients granted `audit:read`.
3. Confirm `.well-known/openid-configuration` no longer advertises `audit:read`.

The audit-api service itself remains deployed but every request returns 401 once the
authority no longer issues tokens for `audit:read`.

---

## Promotion Checklist

When promoting a deploy from test to stage, then to production:

- [ ] Sign in to **stage** admin UI (`auth.mattgerega.org`)
- [ ] Repeat sections 1, 2, 3 above using the same field values
- [ ] Run the verify-curl against the stage discovery endpoint
- [ ] Sign in to **production** admin UI (`auth.mattgerega.com`)
- [ ] Repeat sections 1, 2, 3 above
- [ ] Run the verify-curl against the production discovery endpoint

The configuration does **not** replicate from test automatically. Each env is a
manual, identical pass.
