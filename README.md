# EnterpriseDataManager - Archiver Solution Skeleton

This solution implements a layered ASP.NET Core 8 MVC + API application with DI, EF Core persistence, background scheduling, and security-conscious defaults.

## Architecture (Layered)

```
+-------------------+    +-------------------+    +-------------------------+
| Presentation      |    | API Controllers   |    | Background Services     |
| - Razor MVC       |    | - JSON endpoints  |    | - ArchivalJobScheduler  |
+---------+---------+    +---------+---------+    +-----------+-------------+
          |                         |                          |
          v                         v                          v
+-------------------+    +-------------------+    +-------------------------+
| Application Layer | <--| MediatR/Services |---> | Security/Policy Hooks   |
| - IArchivalService|    | - IRecoveryService|    | - IRansomwareProtector  |
| - IPolicyEngine   |    | - IIdamConnector  |    | - ISiemForwarder        |
+---------+---------+    +---------+---------+    +-----------+-------------+
          |                         |                          |
          v                         v                          v
+-------------------+    +-------------------+    +-------------------------+
| Domain Models     |    | Repositories      |    | Infrastructure Adapters |
| - ArchivePlan     |    | - IRepository<T>  |    | - IStorageProvider      |
| - ArchiveJob/Item |    | - IUnitOfWork     |    |   - LocalFilesystem     |
| - RecoveryJob     |    | - EF Core         |    |   - S3Compatible (stub) |
| - RetentionPolicy |    |                   |    |   - TapeDevice (sim)    |
+-------------------+    +-------------------+    +-------------------------+
```

## Security & Compliance Best-Practice Checklist

- [x] HTTPS-only with HSTS in non-dev
- [x] Minimal CORS, explicit allowed origins
- [x] CSP headers deny by default; no inline scripts
- [x] ASP.NET Core Identity enabled; OIDC integration points wired
- [x] Secrets via environment variables (e.g., ARCHIVER_CONN_STR, ARCHIVER_ENC_KEY)
- [x] Structured logging (Serilog) with SIEM forwarder hook
- [x] Ransomware mitigations: encryption support, WORM simulation, anomaly hooks, MFA gates
- [x] Central policy engine for retention, access decisions
- [x] Health endpoint at /healthz
- [x] Containerization with Docker; compose includes Postgres + ELK stubs
- [x] CI: build, test, dependency scan, Docker image publish

## Prerequisites

- .NET 8 SDK
- Docker + Docker Compose
- Postgres (via docker-compose)
- EF Core tools: `dotnet tool install --global dotnet-ef`

## Environment Variables

- ARCHIVER_CONN_STR: Postgres connection string (e.g., Host=postgres;Port=5432;Database=archiver;Username=archiver;Password=archiver)
- ARCHIVER_ENC_KEY: 32-byte Base64 or hex key for AES-256 encryption
- OIDC_AUTHORITY: OIDC authority URL (e.g., https://login.example.com/)
- OIDC_CLIENT_ID: OIDC client ID
- OIDC_CLIENT_SECRET: OIDC client secret
- ALLOWED_ORIGINS: Comma-separated list of allowed CORS origins
- ARCHIVAL_CRON: Cron expression for archival job scheduler (e.g., "0 */6 * * *")

## EF Core Migrations

Run:
- `dotnet ef migrations add Initial --project src/Archiver.Infrastructure --startup-project src/Archiver.Web`
- `dotnet ef database update --project src/Archiver.Infrastructure --startup-project src/Archiver.Web`

## Running locally

- `docker compose up -d` (starts postgres and web)
- Or `dotnet run --project src/Archiver.Web`

Visit:
- UI: https://localhost:8443/
- Health: https://localhost:8443/healthz
- API: https://localhost:8443/api/archive

## Notes

- S3/Tape providers are stubs/simulators; integrate vendor SDKs for production.
- Background scheduler uses NCrontab; persisted "last run" tracking can be added.
- Integration points for EDR/IPS/VPN/firewall are placeholders; add adapters as needed.
- Identity + OIDC wiring is provided; configure your IdP in environment variables.
