# SmartDocScan PostgreSQL Migration

This migration keeps the current SQL Server production database untouched while loading a copy into PostgreSQL on the Ubuntu app server.

## 1. Configure Environment

Set these values in `.env` on the Ubuntu server:

```env
SMARTDOCSCAN_POSTGRES_DB=smartdocscan
SMARTDOCSCAN_POSTGRES_USER=smartdocscan
SMARTDOCSCAN_POSTGRES_PASSWORD=replace-with-long-random-password
SMARTDOCSCAN_POSTGRES_PORT=5432
SMARTDOCSCAN_POSTGRES_CONNECTION_STRING=postgresql://smartdocscan:replace-with-long-random-password@postgres:5432/smartdocscan
SMARTDOCSCAN_SQLSERVER_CONNECTION_STRING=mssql://sql_user:sql_password@192.168.148.15:1433/dms
```

For the PostgreSQL beta build, point the API connection string at the Postgres service:

```env
SMARTDOCSCAN_CONNECTION_STRING=Host=postgres;Port=5432;Database=smartdocscan;Username=smartdocscan;Password=replace-with-long-random-password
```

## 2. Start PostgreSQL

```bash
sudo docker compose up -d postgres
sudo docker compose ps postgres
```

## 3. Run Test Migration

```bash
sudo docker compose -f docker-compose.yml -f docker-compose.pgloader.yml --profile migrate run --rm pgloader
```

## 4. Verify Counts

```bash
sudo docker compose exec postgres psql -U "$SMARTDOCSCAN_POSTGRES_USER" -d "$SMARTDOCSCAN_POSTGRES_DB"
```

Then run:

```sql
select 'company' as table_name, count(*) from company
union all select 'patient', count(*) from patient
union all select 'documents', count(*) from documents
union all select 'usersinfo', count(*) from usersinfo
union all select 'audit_log', count(*) from audit_log;
```

## 5. Cutover

After migration is verified, start the beta app on PostgreSQL:

```bash
sudo docker compose up -d --build
```
