# audit chart

Combined OCI chart for the Audit application's own controllers: `audit-api` (read API) and `audit-processor` (RabbitMQ -> MongoDB worker). Published from this repo (`ghcr.io/spydersoft-consulting/charts/audit`), versioned alongside the container images.

This chart does **not** own or create any Kubernetes `Secret`, `ConfigMap`, or backing infrastructure (MongoDB, RabbitMQ). It only references config/secrets **by name**, via `envFrom.secretRef`/`envFrom.configMapRef`, with the names themselves overridable values. Whoever composes this chart (today: `platform-helm-config`) is responsible for creating the referenced Secret/ConfigMap and for owning the shared MongoDB/RabbitMQ instances audit connects to.

## Values

- `controllers.audit-api.containers.main.image.tag` — audit-api image tag.
- `controllers.audit-processor.containers.main.image.tag` — audit-processor image tag.
- `controllers.audit-api.containers.main.envFrom` / `controllers.audit-processor.containers.main.envFrom` — supplied entirely by the caller; not defaulted here (every real caller overrides this in full to add its own `configMapRef`s — see the secrets contract below for what the secret must contain).
- `route.audit-api.hostnames` — per-environment hostname(s); not defaulted here since every real caller supplies them.

## Secrets contract

The caller must create a secret named **`audit-secrets`** containing:

- `RabbitMq__Username` / `RabbitMq__Password`
- `ConnectionStrings__audit-mongo`

The secret name is not hardcoded in this chart — it's supplied via the caller's `envFrom.secretRef.name` override, so a different composing repo could name/source it however it wants without any chart change.
