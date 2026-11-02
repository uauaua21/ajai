# Architecture

## Scope

The supplied pitch deck describes four parts: a .NET server, Vue 3 dashboard, Python/Flask YOLO11 NCNN inference worker, and edge camera devices. Only the server source was present in the original folder. A replacement Python/Flask worker has since been added under `ai/`, using a pinned public YOLO11n ONNX model. It supports real still-image detection and authenticated alert delivery. The original Vue dashboard and live camera client remain absent.

## Request and event flow

1. A user or camera submits email and password to `AuthController`.
2. BCrypt verifies the password. The server signs a one-hour JWT containing the email identity and a `User` or `Camera` role.
3. A camera sends a typed alert, base64 payload, and its own email to `AlertsController`. The API validates the request and requires that the email matches its JWT identity.
4. EF Core saves the alert to SQLite. SignalR then broadcasts `ReceiveNewAlert` to the `Admins` group.
5. Dashboard users retrieve recent alerts and safe camera fields through REST.
6. A dashboard user toggles a camera. The API saves the state and emits `CameraStatusChanged` to `Admins` and `ReceiveCameraCommand` to the matching camera group.

Database commits happen before broadcasts. Delivery is best effort. If a broadcast fails, the saved row remains. Clients should refresh through REST after reconnecting. There is no transactional outbox or replay cursor.

## Data model

| Entity | Fields | Meaning |
| --- | --- | --- |
| User | Id, Email, Password | Dashboard account; Password is a BCrypt hash |
| Camera | Id, Email, Password, IsActive, LastPing | Camera identity and requested state; Password is a BCrypt hash |
| Alert | Id, CameraEmail, AlertType, Base64Image, CreatedAt | External detection event and UTC timestamp |

The schema preserves the original hackathon tables. CameraEmail is a string reference with no foreign key. Account provisioning is outside the API. The optional development seeder uses reserved `example.test` addresses and caller-supplied passwords.

## Authorization

`User` is the dashboard operator role. There is no separate administrator hierarchy. Users can list all cameras, read all recent alerts, and toggle any camera. Cameras can only submit their own alerts and join their own command group. A camera cannot read the global alert history or join the dashboard group.

The hub requires authentication. `JoinDashboard` requires `User`. `JoinCameraGroup` requires `Camera` and checks the requested email against the authenticated identity. Tokens in the `access_token` query parameter are accepted only on `/dashboardHub` for browser transport compatibility. Hosts and proxies must avoid logging those query strings. Connections close when their authentication expires.

## Design choices

- SQLite keeps a local prototype simple. It is not a multi-instance storage design.
- EF migrations create the schema on startup. Controlled deployment migrations are preferable for production.
- Secrets stay outside tracked configuration. Missing or short signing keys stop startup.
- Development seeding replaces credential-bearing migration seed data. A fresh database is required for this cleaned release.
- CORS uses explicit origins. It does not replace authentication.
- Alert bodies are limited to 1.5 MB; the base64 field is limited to 1.4 million characters. Validation checks base64 syntax, not whether the bytes are a safe image.
- Login allows ten attempts per minute per remote IP. A trusted proxy setup must preserve and validate the correct client IP before this limiter can work per client behind a proxy.

## Next steps

1. Move to .NET 10 LTS and current compatible dependency patches.
2. Recover or build the external dashboard and live camera client. Extend the included still-image worker only after evaluating its model.
3. Add account provisioning, unique email constraints, token revocation, and credential rotation.
4. Add camera heartbeats and command acknowledgments.
5. Move images to protected object storage with a retention policy.
6. Add durable event delivery, structured audit logs, and deployment monitoring.
7. Evaluate a detection model with a documented dataset, metrics, and failure cases before making AI performance claims.
