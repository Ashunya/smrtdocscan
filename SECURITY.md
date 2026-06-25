# SmartDocScan Security Overview

Last updated: June 25, 2026

This document summarizes the current SmartDocScan security posture for customer architecture and security review. It is intended as product security evidence, not as a legal certification or HIPAA attestation.

## Summary

SmartDocScan is a web application for storing, retrieving, scanning, and managing medical document records. The current deployment is a React frontend, .NET 8 API, Microsoft SQL Server database, Caddy reverse proxy, and Docker containers.

SmartDocScan should be deployed only over HTTPS. Public references to "128-bit SSL" should be considered outdated and should be replaced with current TLS wording:

- Public web traffic is protected using HTTPS with modern TLS.
- The production reverse proxy uses Let's Encrypt certificates through Caddy.
- Deprecated TLS 1.0 and TLS 1.1 are not accepted by the current public endpoint.
- TLS 1.2 is accepted by the current public endpoint.

Live verification on June 25, 2026 showed:

- `https://scan.ashunya.com` responded over HTTPS.
- TLS 1.0 failed negotiation.
- TLS 1.1 failed negotiation.
- TLS 1.2 successfully negotiated.
- The response included `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Resource-Policy`, and `X-Permitted-Cross-Domain-Policies`.

## Authentication

SmartDocScan supports:

- Local username and password authentication.
- Microsoft Entra ID SSO using OpenID Connect.
- Multitenant customer SSO through company tenant mappings.

Authentication uses secure server-side ASP.NET cookie sessions:

- Cookie name: `smartdocscan.session`
- `HttpOnly` enabled
- `Secure` required outside development
- `SameSite=Lax`
- Sliding expiration
- Server-side sign-out support

Local password authentication includes login rate limiting and login-attempt tracking when the supporting table is installed.

## Authorization And Tenant Boundaries

SmartDocScan enforces company boundaries in API operations. Users are associated with a company, while admin and super admin behavior is handled through explicit permission claims.

Supported permissions include document upload, scan, delete, print, download, category management, user management, patient management, box management, reports, admin, and super user.

Document preview and download operations require authenticated users and enforce tenant access checks.

## Audit Logging

SmartDocScan records security-sensitive activity in audit logs, including authentication, patient changes, document activity, settings updates, and forbidden access attempts.

Audit logs are available in the application for authorized administrators. Audit retention cleanup is implemented through configurable security settings.

## Transport Security

Recommended production deployment:

- Caddy terminates HTTPS using Let's Encrypt.
- HTTP is redirected to HTTPS.
- HSTS is enabled on web and API hostnames.
- API CORS is restricted to the configured frontend origin.
- Cross-site unsafe requests are rejected unless they come from an allowed origin or the Microsoft SSO callback.

Current production hostnames:

- Frontend: `https://scan.ashunya.com`
- API: `https://scanapi.ashunya.com`
- Microsoft redirect URI: `https://scanapi.ashunya.com/api/auth/microsoft/callback`

## Data Storage

SmartDocScan stores metadata in Microsoft SQL Server and document files in a configured document store path.

Production recommendations:

- Restrict SQL Server network access to the application server only.
- Use a least-privileged SQL login for the application.
- Enable SQL Server transport encryption where supported by the customer SQL Server configuration.
- Back up SQL Server, the document store, Caddy certificate data, and ASP.NET data protection keys.
- Protect the document store with operating-system permissions and server backup controls.

## Browser Security Headers

The production Caddy configuration sends these browser hardening headers:

- `Content-Security-Policy`
- `Strict-Transport-Security`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy`
- `Cross-Origin-Resource-Policy: same-site`
- `X-Permitted-Cross-Domain-Policies: none`
- `Cache-Control: no-cache, no-store, must-revalidate`
- `Pragma: no-cache`
- `Expires: 0`

The API also sends matching security headers for API responses.

The frontend CSP is intentionally compatible with SmartDocScan scanning workflows. It permits same-origin assets, the SmartDocScan API, Dynamsoft licensing endpoints, local Dynamsoft scanner service communication, blob/data previews, and Microsoft sign-in form navigation. The API hostname uses a restrictive `default-src 'none'` policy because it serves API responses rather than a browser application.

The production proxy also removes `ETag`, `Last-Modified`, `Server`, `Via`, and `X-Powered-By` response headers where possible to reduce caching, timestamp disclosure, and technology fingerprinting signals.

## Operational Security Requirements

For a healthcare deployment, the customer and hosting operator should validate:

- HTTPS is active and certificates are current.
- DNS points only to approved infrastructure.
- Database access is limited to approved hosts.
- Backups are encrypted and access controlled.
- Administrator accounts are restricted and reviewed.
- Audit logs are retained according to customer policy.
- Microsoft Entra ID SSO is configured with the approved multitenant app registration.
- Vendor and customer have agreed on HIPAA/BAA responsibilities where applicable.

## Items For Security Review

Recommended review artifacts to provide to SCA:

- This security overview.
- Current Caddyfile and Docker Compose deployment files.
- Screenshot or command output showing TLS 1.0/1.1 rejected and TLS 1.2 accepted.
- Screenshot or command output showing security response headers.
- Microsoft Entra app registration redirect URI and supported account type.
- Audit log screenshot.
- Database least-privilege role screenshot.
- Backup and restore procedure.
- Data retention policy.

## Important Notes

Do not describe SmartDocScan as using "128-bit SSL." SSL is deprecated terminology. Use "HTTPS with modern TLS" or "TLS 1.2 or newer, with managed public certificates" instead.

This document does not certify HIPAA compliance by itself. HIPAA readiness depends on the hosting environment, access controls, backup procedures, audit retention, customer policies, and any required Business Associate Agreement.
