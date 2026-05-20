# SmartDocScan Deployment Guide

This guide covers the recommended production deployment for the React + .NET SmartDocScan app on a Linux server using Docker Engine and Caddy.

## Recommended Architecture

```text
Linux server
  Docker Engine
  Caddy reverse proxy with Let's Encrypt
  SmartDocScan Web container
  SmartDocScan API container
  Document store mounted at /data/smartdocscan/store

SQL Server remains external
```

This avoids Docker Desktop, WSL IP changes, Windows portproxy, and NSSM startup issues.

## Server Requirements

- Ubuntu 22.04/24.04 or Debian 12
- Docker Engine and Docker Compose plugin
- Public inbound TCP ports `80` and `443`
- DNS records:
  - `scan.ashunya.com` pointing to the Linux server public IP
  - `scanapi.ashunya.com` pointing to the Linux server public IP
- Network access from Linux to SQL Server on TCP `1433`
- Document store available to the Linux server

## Install Docker

```bash
sudo apt update
sudo apt install -y ca-certificates curl git
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
newgrp docker
```

Verify:

```bash
docker version
docker compose version
```

## Clone The App

```bash
mkdir -p ~/docker
cd ~/docker
git clone https://github.com/Ashunya/smrtdocscan.git
cd smrtdocscan
```

## Environment File

Create `~/docker/smrtdocscan/.env`:

```env
SMARTDOCSCAN_CONNECTION_STRING=Server=192.168.142.15,1433;Database=dms;User Id=dms_fill;Password={YOUR_PASSWORD};TrustServerCertificate=True;Encrypt=False;
SMARTDOCSCAN_STORE_PATH=/data/smartdocscan/store
SMARTDOCSCAN_COOKIE_DOMAIN=.ashunya.com
SMARTDOCSCAN_DEFAULT_COMPANY_ID=7
```

Notes:

- Keep `SMARTDOCSCAN_CONNECTION_STRING` on one line.
- If the SQL password contains a semicolon, keep the password wrapped in braces: `Password={...};`.
- If the password contains `}`, escape it as `}}`.
- Microsoft SSO and SMTP settings are configured from the app Settings page, not from Docker Compose.

## Document Store

The app expects the existing store folder structure to remain unchanged:

```text
/data/smartdocscan/store/{company_id}/{patient_id}/{category_id}_{document_name}
```

Example:

```text
/data/smartdocscan/store/7/263053/22_GRECO JENNIFER L 1-12-26.tif
```

### Option A: Local Linux Disk

Recommended for reliability.

```bash
sudo mkdir -p /data/smartdocscan/store
sudo chown -R $USER:$USER /data/smartdocscan
```

Copy the existing `D:\DMS\store` contents into:

```text
/data/smartdocscan/store
```

### Option B: Mount Windows Share

If the files must stay on Windows, share `D:\DMS\store` and mount it with CIFS:

```bash
sudo apt install -y cifs-utils
sudo mkdir -p /data/smartdocscan/store
```

Create a credentials file:

```bash
sudo nano /etc/smartdocscan-smb.credentials
```

```text
username=WINDOWS_USERNAME
password=WINDOWS_PASSWORD
domain=WORKGROUP
```

Secure it:

```bash
sudo chmod 600 /etc/smartdocscan-smb.credentials
```

Add to `/etc/fstab`:

```fstab
//192.168.142.15/DMS/store /data/smartdocscan/store cifs credentials=/etc/smartdocscan-smb.credentials,iocharset=utf8,file_mode=0777,dir_mode=0777,noperm 0 0
```

Mount:

```bash
sudo mount -a
ls /data/smartdocscan/store
```

## Caddyfile

Create `~/docker/smrtdocscan/Caddyfile`:

```caddyfile
scan.ashunya.com {
    reverse_proxy web:80
}

scanapi.ashunya.com {
    reverse_proxy api:8080
}
```

## Production Compose File

Use this structure in `docker-compose.yml`:

```yaml
services:
  api:
    build:
      context: .
      dockerfile: SmartDocScan.Api/Dockerfile
    container_name: smrtdocscan-api
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      ConnectionStrings__SmartDocScan: ${SMARTDOCSCAN_CONNECTION_STRING}
      Cors__AllowedOrigins__0: https://scan.ashunya.com
      Store__RootPath: /data/store
      Authentication__CookieDomain: ${SMARTDOCSCAN_COOKIE_DOMAIN:-}
    volumes:
      - ${SMARTDOCSCAN_STORE_PATH}:/data/store
      - ./DataProtection-Keys:/root/.aspnet/DataProtection-Keys
    restart: unless-stopped

  web:
    build:
      context: .
      dockerfile: SmartDocScan.Web/Dockerfile
      args:
        VITE_API_BASE_URL: https://scanapi.ashunya.com/api
        VITE_DEFAULT_COMPANY_ID: ${SMARTDOCSCAN_DEFAULT_COMPANY_ID:-7}
    container_name: smrtdocscan-web
    depends_on:
      - api
    volumes:
      - ./SmartDocScan/Resources:/usr/share/nginx/html/Resources:ro
    restart: unless-stopped

  caddy:
    image: caddy:2
    container_name: smrtdocscan-caddy
    depends_on:
      - api
      - web
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - ./CaddyData:/data
      - ./CaddyConfig:/config
    restart: unless-stopped
```

## Start The App

```bash
docker compose config
docker compose up -d --build
docker compose ps
```

Open:

```text
https://scan.ashunya.com
```

## Updating The App

```bash
cd ~/docker/smrtdocscan
git pull origin main
docker compose up -d --build api web
```

If Docker Compose panics during build because of the bake builder, use:

```bash
COMPOSE_BAKE=false docker compose up -d --build api web
```

## Microsoft SSO

Configure Microsoft SSO from the SmartDocScan Settings page as a super admin.

Azure app registration should be configured as:

- Supported account types: multiple organizations
- Redirect URI:

```text
https://scanapi.ashunya.com/api/auth/microsoft/callback
```

Customer tenants are added from the company create/edit page.

## SMTP

SMTP is also configured from the SmartDocScan Settings page. Do not put SMTP secrets in `docker-compose.yml`.

## Useful Commands

```bash
docker compose ps
docker compose logs --tail=100 api
docker compose logs --tail=100 web
docker compose logs --tail=100 caddy
docker compose restart api
docker compose restart web
docker compose restart caddy
```

Test DNS:

```bash
nslookup scan.ashunya.com
nslookup scanapi.ashunya.com
```

Test SQL connectivity:

```bash
nc -vz 192.168.142.15 1433
```

If `nc` is missing:

```bash
sudo apt install -y netcat-openbsd
```

## Common Issues

### Caddy Cannot Get A Certificate

Check:

- DNS points to this Linux server public IP.
- Ports `80` and `443` are open from the internet.
- No other process is using ports `80` or `443`.

```bash
sudo ss -tulpn | grep -E ':80|:443'
docker compose logs --tail=100 caddy
```

### SQL Connection String Format Error

If logs show:

```text
Format of the initialization string does not conform to specification
```

Check `.env`:

- connection string is one line
- no stray `>` characters
- password with semicolon is wrapped: `Password={...};`
- `TrustServerCertificate=True`
- `Encrypt=False` if required by the SQL Server

### Document File Not Found

Check that `SMARTDOCSCAN_STORE_PATH` points to the real document store and the folder layout matches the database `url` column.

Example database URL:

```text
7/263053/22_GRECO JENNIFER L 1-12-26.tif
```

Expected file:

```text
/data/smartdocscan/store/7/263053/22_GRECO JENNIFER L 1-12-26.tif
```

### Browser Still Shows Old Login State

Clear cookies for:

```text
ashunya.com
scan.ashunya.com
scanapi.ashunya.com
```

This is common after moving between Docker Desktop, WSL, and Linux because ASP.NET data protection keys/cookies change.

## Backup

Back up these items:

- SQL Server `dms` database
- document store folder
- `.env`
- `DataProtection-Keys`
- `CaddyData`

Example:

```bash
tar -czf smartdocscan-config-backup.tgz .env DataProtection-Keys CaddyData CaddyConfig Caddyfile docker-compose.yml
```
