# SmartDocScan React + .NET Migration

This migration keeps the existing SQL Server database and adds a new containerized application beside the legacy MVC app.

## Current Scope

Implemented slices:

- .NET 8 API project: `SmartDocScan.Api`
- React + Vite frontend: `SmartDocScan.Web`
- Existing `patient` table support:
  - Search by `patient_id`, `pext_id`, first name, and last name
  - Add patient
  - Edit patient
  - Duplicate `pext_id` check per company
- Existing `box` table support:
  - List boxes by company
  - Add box
  - Duplicate `box_ext_id` check
- Existing `category` and `documents` table support:
  - List categories by company
  - List patient documents grouped by category
  - Upload patient documents to `Store/{companyId}/{patientId}/`
  - Download and delete patient documents
- Existing `company` and `usersinfo` table support:
  - Sign in with existing username/password rows
  - Company selection
  - User list and permission editing
- Dockerfiles for API and web
- `docker-compose.yml` for local/container deployment

Not yet migrated:

- Server-enforced authorization/session tokens
- Add/edit company form
- Dynamsoft scanner flow
- Reports

## Configure

Create an environment file or set these variables before running Docker:

```powershell
$env:SMARTDOCSCAN_CONNECTION_STRING='Server=YOUR_SQL_SERVER;Database=dms;User Id=YOUR_SQL_USER;Password=YOUR_SQL_PASSWORD;TrustServerCertificate=True;'
$env:SMARTDOCSCAN_DEFAULT_COMPANY_ID='7'
```

Or copy `.env.example` to `.env` and edit the values:

```powershell
Copy-Item .env.example .env
notepad .env
```

For Windows SQL Server from Linux containers, use a reachable server name/IP, not `localhost`, unless SQL Server is running inside the same compose network.

## Run With Docker

```powershell
docker compose build
docker compose up -d
```

Open:

- React app: `http://localhost:8088`
- API health: `http://localhost:5080/health`

## API Endpoints

```http
GET /api/patients?companyId=7&search=166563
GET /api/patients/{patientId}
POST /api/patients
PUT /api/patients/{patientId}
GET /api/boxes?companyId=7
POST /api/boxes
GET /api/categories?companyId=7
GET /api/documents?companyId=7&patientId=265127
POST /api/documents
GET /api/documents/{documentId}/download
DELETE /api/documents/{documentId}
GET /api/companies
GET /api/users?companyId=7
POST /api/users
POST /api/auth/login
```

## Next Recommended Phase

1. Add server-enforced authorization/session tokens.
2. Add company create/edit.
3. Add thumbnail/card document view.
4. Move Dynamsoft scanner last, after API upload/document flows are stable.
