# SmartDocScan Project Context

This is the active clean project folder for SmartDocScan. The older OneDrive checkout contains legacy MVC files and should not be used for new work unless explicitly needed for reference.

## Active Project Path

- Active folder: `C:\MyProjects\smartdocscan`
- Git remote: `https://github.com/Ashunya/smrtdocscan.git`
- Branch: `main`

## Current Architecture

- Frontend: React/Vite app in `SmartDocScan.Web`
- Backend: .NET 8 API in `SmartDocScan.Api`
- Database: Existing SQL Server `dms` database, schema preserved where possible
- Document storage: External store mounted into the API container at `/data/store`
- Reverse proxy/TLS: Caddy using `Caddyfile`
- Deployment: Docker Compose with `api`, `web`, and `caddy` services

## Important Deployment Files

- `docker-compose.yml`
- `Caddyfile`
- `.env.example`
- `DEPLOYMENT.md`
- `SECURITY.md`
- `SmartDocScan.Api/Database/Schema.sql`
- `SmartDocScan.Api/Database/SecuritySetup.sql`

## Dynamsoft / Scanner Rules

- Do not remove or aggressively tighten CSP values that Dynamsoft needs.
- The app intentionally keeps CSP allowances such as `unsafe-inline` and `unsafe-eval` because Dynamsoft Web TWAIN requires them.
- Dynamsoft resources are external runtime files, not copied into the web image.
- Docker Compose mounts `./SmartDocScan/Resources` to `/usr/share/nginx/html/Resources`.
- Keep Dynamsoft 19.3 resource files in `SmartDocScan/Resources` on the deployment host.
- The scanner key is configured in `SmartDocScan/Resources/dynamsoft.webtwain.config.js`.
- Rebuild/recreate containers after changing Dynamsoft resources so browser-served files update.

## Database Rules

- Do not let the app modify production schema by default.
- Runtime schema creation is gated by `Database:AutoEnsureSchema`.
- Default config should keep `Database:AutoEnsureSchema` disabled.
- Use `SmartDocScan.Api/Database/Schema.sql` as the one-time DBA setup script for required support tables/columns.
- Existing production data and table names should be preserved.

## Security Decisions

- Authentication uses secure server-side cookies, not localStorage JWTs.
- SSO is Microsoft Entra multitenant.
- Local users are still supported.
- MFA/email OTP was removed/deferred for now.
- SSO configuration is managed from app settings where possible.
- Audit logging is available and should be retained for security-sensitive operations.
- Preview/download permissions should remain tenant-bound and permission-aware.
- Super admin/admin behavior must not bypass tenant safety except where explicitly required for cross-company administration.
- Caddy adds security headers for frontend/API traffic.

## Docker / Production Notes

- Use Docker Desktop or a Linux server for deployment.
- If using Windows paths, document storage usually maps like:
  - Host: `D:/DMS/store`
  - Container: `/data/store`
- API default internal port: `8080`
- Web default internal port: `80`
- Caddy exposes public ports `80` and `443`.
- Public app URLs:
  - Web: `https://scan.ashunya.com`
  - API: `https://scanapi.ashunya.com`

## Current Clean Folder Intent

The clean folder intentionally excludes:

- Old ASP.NET MVC `SmartDocScan` application code
- `CoreLibrary`
- legacy `packages`
- `.vs`
- `bin`
- `obj`
- `node_modules`
- frontend `dist`
- generated or build output

Git may show old legacy files as deleted in this clean working tree. That is expected if committing the cleanup, but confirm before staging or pushing broad deletions.

## Verification Commands

Run from `C:\MyProjects\smartdocscan`:

```powershell
dotnet build .\SmartDocScan.Api\SmartDocScan.Api.csproj --source https://api.nuget.org/v3/index.json
cd .\SmartDocScan.Web
npm.cmd ci
npm.cmd run build
npm.cmd audit --omit=dev
cd ..
docker compose config
docker compose build api web
```

## Working Rule

Before security or UI changes, preserve:

- scanner initialization
- scan preview
- save as PDF/TIF
- document upload
- document preview/download
- category selection
- company/tenant boundary

If a security header, package update, or refactor risks breaking Dynamsoft, test scanner behavior before pushing.
