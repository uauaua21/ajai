# Security

This is a hackathon backend prototype. Do not expose it to real workplace camera feeds until deployment, privacy, and access controls have been reviewed.

## Local configuration

- Set `Jwt:Key` with user-secrets or `Jwt__Key` in the hosting environment. Use at least 32 random bytes. Never commit real signing keys, passwords, tokens, databases, or footage.
- Treat the original hard-coded signing key and demo credentials as exposed. Replace them in any existing environment; removing them from this copy does not revoke old credentials.
- Use a fresh database. Legacy demo accounts and password hashes were removed from the migration source, not from any existing databases.
- The demo seeder runs only in Development and only when enabled. It never resets existing account passwords.

## Before deployment

Use HTTPS end to end, a secret manager, real account provisioning, restricted origins/hostnames, and controlled database migrations. Configure trusted proxies before accepting forwarded headers. Review login rate limiting behind that proxy. Do not log Authorization headers, alert image bodies, passwords, or SignalR query tokens.

One-hour JWTs have no revocation or refresh support. Roles are trusted until expiration. `IsActive` is a requested stream state; it does not revoke a camera's ability to post alerts. Startup migration and demo setup are intended for a single local instance.

Images remain base64 text in SQLite with no retention policy, malware scanning, or file-type validation. Define who can see the data and when it must be deleted. Avoid committing images of identifiable people or workplace footage to GitHub.

## Reporting an issue

Do not put credentials or private footage in a public issue. Use GitHub's private vulnerability reporting feature if the repository owner has enabled it. Otherwise contact the maintainer privately through a verified channel before sharing sensitive details.

## Replacement AI worker

The worker is local by default. Its HTTP mode requires `AJAI_WORKER_KEY`, limits uploads, and accepts only JPEG/PNG inputs. It does not enable browser CORS or remote binding. Do not expose Flask's development server publicly. Camera credentials are environment-only; remote backend delivery requires HTTPS and rejects redirects. Model weights are checksum-verified before inference. The original image is re-encoded before alert delivery, but its visible contents may still be private. Only send images you are authorized to process.

Object detection can miss violations and produce false warnings. Require human review. No detection is not proof of safety. The model is external and retains its own AGPL-3.0 licensing terms.
