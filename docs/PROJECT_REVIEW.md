# Project review

Review date: 28 October 2026.

## What the project does

AJAI is a workplace safety monitoring concept. The pitch deck proposes using YOLO11 to detect PPE violations, then showing alerts through a Vue dashboard. The originally supplied source implements the ASP.NET Core backend that connects accounts, cameras, stored alerts, and real-time command channels.

The backend has three controllers (`Auth`, `Cameras`, `Alerts`), three persistent entities (`User`, `Camera`, `Alert`), one SignalR hub, and a SQLite migration. The originally supplied folder did not include an AI inference client, a dashboard, camera capture, or model evaluation. A real replacement inference worker has now been added; its details are recorded below.

## What was supplied

| Material | Finding | Treatment |
| --- | --- | --- |
| AJAI-Server | Main Visual Studio backend project | Source preserved and organized under `src/` |
| untitled folder | Eight source files identical to files in the main project | Excluded as duplicates |
| AJAI_compressed.pdf | Seven image-based pitch slides describing the wider system | Reviewed visually; kept in the original folder |
| Logo PNG | Supplied shield asset | Included as `docs/assets/ajai-logo.png` |
| Screenshots and large generated image | Loose presentation assets | Kept in the original folder |
| SQLite database and journals | Runtime data that may contain accounts or image payloads | Excluded; fresh local database required |
| bin, obj, .vs, user settings | Build and machine-specific output | Excluded and ignored |

No source Git history was present in the supplied project. The prepared repository starts independently. The original folder was not edited.

## Deck versus source

| Deck capability | Present in supplied source? |
| --- | --- |
| Password verification and JWT login | Yes, with insecure defaults in the original |
| SQLite users, cameras, and alerts | Yes |
| Real-time dashboard notifications | Hub and alert event present; original hub lacked authorization |
| Camera start/stop commands | Event publishing present; hardware execution is external |
| Vue 3 / Pinia dashboard | No source supplied |
| Python / Flask / YOLO11 NCNN worker | No source or model supplied |
| Video frames, buffer, three-frame confirmation, cooldown | No corresponding pipeline found |
| Raspberry Pi deployment | No device code or deployment configuration supplied |
| HTTPS deployment on port 5001 | No deployment supplied; original launch profiles use 5102/7233 |

Claims about model accuracy, reliable incident prevention, or detection latency cannot be established from this folder.

## Changes made for GitHub preparation

| Original issue | Change |
| --- | --- |
| Signing secret stored in appsettings | Removed; startup requires an external signing key |
| Weak default passwords and hashes in migration seeds | Removed; optional Development-only seeding uses chosen passwords |
| Random BCrypt values created while building the EF model | Removed dynamic model seeds and the warning suppression |
| Database setup required prior manual work | Startup applies the schema migration to a fresh database |
| Tokens did not carry enforceable roles | Added role claims and controller/hub authorization |
| Cameras could use dashboard routes and impersonate other cameras | Restricted API actions and checked alert/group identities |
| Hub open to anonymous clients | Required authentication and authorized group membership |
| All connected clients received full alert payloads | Alerts now go to the dashboard group only |
| Camera listing exposed password hashes | Replaced entity output with a safe field projection |
| Year-long camera JWTs | Both account types now use one-hour tokens |
| Issuer and audience validation disabled | Enabled both, with algorithm and expiration validation |
| Unlimited login attempts | Added an IP-based login rate limiter |
| No input or request-size checks | Added DTO validation and alert payload limits |
| Open CORS and public Swagger | Configured explicit origins; Swagger is Development-only |
| Mixed local timestamps | New alert timestamps and login pings use UTC |
| Older dependencies | Updated Microsoft packages to 9.0.22 and SQLite bundle to 2.1.14 |
| No repeatable verification | Added integration tests, dependency lock files, and CI |
| No project documentation | Added README, API reference, architecture, contribution, and security notes |

The dependency audit initially reported SQLite and SignalR advisories. Package updates address the versions reported by NuGet. Sources: [SQLite advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) and [Microsoft SignalR advisory](https://github.com/advisories/GHSA-334f-vvp4-7g6p). A clean advisory scan is not a complete security assessment.

## Compatibility changes

Existing clients must log in again to obtain tokens with roles, issuer, and audience. A camera token cannot read global alerts or cameras. Only a camera token can create alerts. Only a User token can join the dashboard group or toggle cameras. Clients must renew tokens after one hour. The existing route names, group method names, and event names were retained.

Use a new database. The migration ID was retained but its credential seed data was removed. Reusing an already migrated database will retain its old accounts. Rotating the signing key invalidates previous tokens, but it does not reset stored passwords.

## Remaining limits

This is suitable for a backend portfolio repository, not a complete AI demo or production safety system. The missing dashboard and live camera client must be recovered or implemented separately. A replacement still-image AI worker is now included; the original model is still lost. There is no account management, heartbeat monitor, token revocation, command acknowledgment, image retention policy, or durable event queue. All dashboard users share the same operator permissions. Review those limits before real use.

The target framework remains .NET 9 to preserve the supplied project's major platform version. Upgrade to .NET 10 LTS before long-term deployment, following Microsoft's [support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

No individual contribution claims, team names, awards, model metrics, or open-source license were invented. Confirm the team's permission before public release, then add accurate credits and an agreed license.

## Verification results

- Release solution build: succeeded with zero warnings and zero errors.
- Integration tests: 10 passed, 0 failed. Includes fresh database startup, missing/short key rejection, production demo/Swagger restrictions, credentials, role boundaries, token tampering, validation, persistence, rate limiting, and SignalR events.
- NuGet audit: no known vulnerabilities reported for direct or transitive dependencies after the updates, as of the review date.
- Locked dependency restore: succeeded with warnings treated as errors.
- Source review: original signing key, copied password hashes, runtime databases, duplicate source files, and IDE/build files excluded.
- GitHub Actions configuration is included. Its hosted run can only be confirmed after upload.

Tests ran locally on macOS arm64 with .NET SDK 9.0.320. These checks do not certify production security or AI detection quality.

## Replacement AI component added

After the initial review, the user requested a public replacement for the lost model. `ai/` now contains a new still-image ONNX inference worker, optional local Flask endpoint, verified model downloader, manifest, license copy, setup guide, model card, and nine Python tests. It uses `yihong1120/Construction-Hazard-Detection` YOLO11n and maps explicit helmet/vest warning classes into the existing AJAI alert API.

Actual model inference, camera authentication, two saved warning types, and dashboard retrieval were verified together. This does not recover the original model, provide the missing Vue dashboard, or establish detection accuracy. See [model provenance](../ai/MODEL_CARD.md).

### Replacement worker validation

- Python 3.13: all nine worker tests passed, including a real ONNX inference call through the authenticated Flask endpoint.
- Actual image inference followed by camera login, alert creation, dashboard login, and database-backed retrieval passed. Both `no_helmet` and `no_vest` rows were verified.
- The final Python lock contains 17 packages. `pip-audit` reported no known vulnerabilities on 28 October 2026.
- The backend solution was rebuilt successfully with zero warnings and zero errors.
- Model downloads and inference outputs are excluded from Git. Model provenance and the upstream license are included.
