# Enterprise Data Manager (EDM)

A production-ready, enterprise-grade data archival and recovery platform built with **ASP.NET Core 8**. EDM provides a full web UI and REST API for managing archive plans, archive jobs, recovery workflows, retention policies, storage providers, and compliance audit trails.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Quick Start — Local Development](#quick-start--local-development)
- [Configuration Reference](#configuration-reference)
- [Database Setup & Migrations](#database-setup--migrations)
- [Running Tests](#running-tests)
- [API Documentation](#api-documentation)
- [Deployment](#deployment)
- [Security](#security)
- [Project Structure](#project-structure)

---

## Overview

EDM covers the full data lifecycle:

- **Archive** — schedule and run archival jobs against configurable storage providers
- **Recover** — guided recovery wizard restores data from archive items; simulation mode isolates dry-runs from live files
- **Retain** — policy engine enforces data retention rules with soft-delete and configurable grace periods
- **Audit** — every write action is logged to the audit trail with actor, timestamp, and outcome
- **Comply** — role-based access, MFA, LDAP/OIDC integration, AES-256-GCM encryption, ransomware protections

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Presentation Layer                        │
│  Razor MVC Pages    │    REST API Controllers    │  Swagger UI  │
└───────────┬─────────────────────┬───────────────────────────────┘
            │                     │
            ▼                     ▼
┌───────────────────────────────────────────────────────────────┐
│                      Application Layer                         │
│  MediatR Commands/Queries  │  Pipeline Behaviours              │
│  IArchivalService          │  IRecoveryService                 │
│  IPolicyEngine             │  IAuditService                    │
└───────────────────────────────────┬───────────────────────────┘
                                    │
            ┌───────────────────────┼───────────────────────┐
            ▼                       ▼                       ▼
┌─────────────────┐   ┌─────────────────────┐   ┌──────────────────────┐
│  Domain (Core)  │   │  Infrastructure     │   │  Background Services │
│  ArchivePlan    │   │  AES-256-GCM Enc    │   │  ArchivalJobScheduler│
│  ArchiveJob     │   │  JWT + Refresh Tkns │   │  RetentionPolicyEnf. │
│  ArchiveItem    │   │  LDAP Connector     │   │  HealthCheckMonitor  │
│  RecoveryJob    │   │  OIDC Connector     │   │  WorkstationAgent    │
│  RetentionPolicy│   │  TOTP MFA (DB-bkd)  │   └──────────────────────┘
│  StorageProvider│   │  Serilog + Seq      │
│  AuditRecord    │   │  NPOI (Excel)       │
│  FilePermission │   │  PdfSharpCore (PDF) │
└─────────────────┘   └─────────────────────┘
                                    │
                                    ▼
                     ┌─────────────────────────┐
                     │  Data Layer (EF Core 8)  │
                     │  SQL Server / PostgreSQL  │
                     │  / SQLite (tests)        │
                     └─────────────────────────┘
```

### Projects

| Project | Role |
|---------|------|
| `EnterpriseDataManager` | ASP.NET Core 8 web host — MVC + API controllers, Razor views, middleware |
| `EnterpriseDataManager.Core` | Domain entities, interfaces, value objects (no external dependencies) |
| `EnterpriseDataManager.Application` | MediatR handlers, application services, pipeline behaviours |
| `EnterpriseDataManager.Infrastructure` | EF Core, encryption, identity connectors, storage providers, background jobs |
| `EnterpriseDataManager.Data` | `DbContext`, EF Core migrations |
| `EnterpriseDataManager.Common` | Shared DTOs, result types, pagination helpers |
| `EnterpriseDataManager.WorkstationAgent` | .NET Worker Service — monitors and syncs workstations |
| `EnterpriseDataManager.UnitTests` | xUnit unit tests |
| `EnterpriseDataManager.IntegrationTests` | xUnit integration + security tests, Testcontainers PostgreSQL |

---

## Features

### Archive Management
- Create and schedule **Archive Plans** with Cronos cron expressions
- Monitor **Archive Jobs** in real time (Pending → Running → Completed / Failed)
- Exponential back-off retry (configurable attempts and initial delay)
- Job persistence via `ScheduledJobRecords` table — survives service restarts
- Semaphore-based concurrency cap (`MaxConcurrentJobs`)

### Recovery
- Guided **Recovery Wizard** UI (step-by-step)
- **Simulation mode** — `IsSimulation = true` routes jobs to a sandbox path; background scheduler skips file writes entirely so simulations cannot overwrite live data
- Track **Recovery Jobs** with status, source archive, and audit trail
- Mobile backup REST endpoints (`/api/v1/mobile-backup/*`)

### MDM Webhook (Intune)
- `POST /api/v1/mobile-backup/mdm-webhook` accepts Intune compliance events
- **HMAC-SHA256** signature validation (`X-Hub-Signature-256`)
- **Timestamp replay protection** — requests outside ±5 minutes are rejected
- Rate-limited under the `auth` policy

### Retention Policies
- Define policies by name, retention days, and trigger conditions
- **Retention Policy Enforcer** runs on a configurable interval with batch processing
- Soft-delete with grace period before permanent removal

### Storage Providers
- **Local filesystem** — direct path, ACL-aware
- **S3-compatible** — AWS, MinIO, Wasabi, or any S3 endpoint
- **Azure Blob Storage** — via Azure SDK
- **Tape Device** — simulation adapter (Windows-only, guarded)
- Provider CRUD UI with integrated connection testing

### Audit & Compliance
- Every create/update/delete is written to `AuditRecords`
- `AuditActionFilter` and `AuditLoggingMiddleware` for automatic capture
- Audit Log UI with date range filter
- File permissions stored in `FilePermissions` table; enforced via `RequireFilePermissionFilter`

### Reporting
- **Excel export** (NPOI) — archive jobs and audit events
- **PDF export** (PdfSharpCore) — same datasets, landscape layout, colour-coded success/failure
- Date range selector on the Reports page
- Endpoints: `GET /api/v1/reports/archive-jobs/excel|pdf`, `GET /api/v1/reports/audit-log/excel|pdf`

### File Sharing
- `IShareService` generates AES-encrypted signed URLs with embedded expiry
- `GET /api/v1/share/{archiveItemId}/signed-url` — requires `Read` permission
- `POST /api/v1/share/{archiveItemId}/permissions` — requires `Admin` permission

### Security
- ASP.NET Core Identity with configurable password policy (14 chars in production)
- **JWT Bearer** for API clients (30-minute tokens); cookie auth for the MVC UI
- **Refresh tokens** — `POST /api/v1/auth/refresh` issues a new access token and rotates the refresh token
- **Token revocation** — `POST /api/v1/auth/logout` invalidates the refresh token by JTI
- **TOTP MFA** — Google Authenticator compatible; state persisted in `MfaStates` DB table (not in-memory)
- **LDAP** and **OIDC** connectors for enterprise IdP integration
- **AES-256-GCM** encryption service for sensitive data at rest
- **HSTS** with `includeSubDomains` + `preload` (max-age 1 year)
- **CSP**, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`
- Global and per-endpoint **rate limiting** (fixed window)
- Account lockout after 3 failed attempts (30-minute window in production)
- Startup guard: throws `InvalidOperationException` in Production if `Jwt:Secret` is empty or pending migrations exist

### Observability
- **Serilog** structured logging — console + rolling daily log files (30-day retention)
- **Seq** integration — enable by setting `Seq:ServerUrl` in config; API key optional
- Health check endpoints: `GET /health`, `GET /healthz`
- **HealthCheckMonitor** background service alerts on consecutive failures
- Notification hooks (email via MailKit, webhooks, configurable)

### Internationalisation
- Full i18n with `.resx` resource files
- Supported: **English** (`en`), **Bulgarian** (`bg`)
- Culture switching via cookie (persisted across requests)

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8, Razor Pages, MVC |
| ORM | Entity Framework Core 8 |
| Databases | SQL Server, PostgreSQL 16, SQLite |
| Messaging | MediatR 12 |
| Scheduling | Cronos |
| Auth | ASP.NET Identity, JWT Bearer, TOTP, LDAP, OIDC |
| Encryption | AES-256-GCM (`System.Security.Cryptography`) |
| Reporting | NPOI 2.7 (Excel), PdfSharpCore 1.3 (PDF) |
| Logging | Serilog 3 with console, file, and Seq sinks |
| Mapping | AutoMapper 15.1 |
| Email | MailKit 4.16 |
| API Docs | Swashbuckle / Swagger UI, Asp.Versioning 8 |
| CI/CD | GitHub Actions (build → SAST → integration tests → Docker → Trivy scan) |
| Containers | Docker multi-stage (linux-x64), docker-compose v2, nginx 1.27 |
| Secrets | HashiCorp Vault (KV v2), or environment variables |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (for local dev) **or** Docker (for PostgreSQL)
- [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`
- Docker + Docker Compose v2 (for containerised deployment)

---

## Quick Start — Local Development

### 1. Clone and restore

```bash
git clone https://github.com/BojidarDermendjiev/EnterpriseDataManager.git
cd EnterpriseDataManager/EnterpriseDataManager
dotnet restore EnterpriceDataManager.sln
```

### 2. Apply database migrations

```bash
dotnet ef database update \
  --project EnterpriseDataManager.Data \
  --startup-project EnterpriseDataManager
```

This creates the `EnterpriseDataManager` SQL Server LocalDB database with all migrations applied.

### 3. Run the application

```bash
dotnet run --project EnterpriseDataManager
```

| Endpoint | URL |
|----------|-----|
| Web UI | `https://localhost:7112` |
| API Documentation (Swagger) | `https://localhost:7112/api-docs` |
| Health check | `https://localhost:7112/health` |

### 4. Create the first admin user

Register at `/Identity/Account/Register`. Promote the first user to Administrator via the database or the admin panel.

---

## Configuration Reference

All settings live in `appsettings.json` (and `appsettings.Production.json` for overrides). Sensitive values must be supplied as **environment variables** or from **HashiCorp Vault** in production — never committed to source control.

### Database

```json
{
  "Database": { "Provider": "SqlServer" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDataManager;Trusted_Connection=True;"
  }
}
```

Supported `Provider` values: `SqlServer`, `PostgreSQL`, `Sqlite`.

### JWT

```json
{
  "Jwt": {
    "Secret": "",
    "Issuer": "EnterpriseDataManager",
    "Audience": "EnterpriseDataManager",
    "ExpiryMinutes": 30
  }
}
```

> **Required in production.** Pass as `Jwt__Secret` environment variable (minimum 32 characters). The app refuses to start in Production if this is empty.

### Seq (optional structured log shipping)

```json
{
  "Seq": {
    "ServerUrl": "http://seq:5341",
    "ApiKey": ""
  }
}
```

Leave `ServerUrl` empty to disable. When set, all Serilog events are forwarded to the Seq instance in addition to console and file sinks.

### CORS

```json
{
  "Cors": {
    "AllowedOrigins": ["https://yourdomain.com"]
  }
}
```

Localhost origins are blocked in Production with a startup warning logged at Critical level.

### Archival Job Scheduler

```json
{
  "ArchivalJobScheduler": {
    "Enabled": true,
    "PollingInterval": "00:01:00",
    "MaxConcurrentJobs": 4,
    "JobTimeout": "04:00:00",
    "MaxJobRetries": 3,
    "InitialRetryDelay": "00:00:30"
  }
}
```

### Retention Policy Enforcer

```json
{
  "RetentionPolicyEnforcer": {
    "Enabled": true,
    "EnforcementInterval": "01:00:00",
    "BatchSize": 100,
    "EnableSoftDelete": true,
    "SoftDeleteGracePeriod": "30.00:00:00",
    "ColdStoragePath": "/mnt/cold-storage"
  }
}
```

### Health Check Monitor

```json
{
  "HealthCheckMonitor": {
    "Enabled": true,
    "CheckInterval": "00:05:00",
    "UnhealthyThreshold": 3,
    "HealthyThreshold": 2,
    "EnableAlerts": true
  }
}
```

### Rate Limiting

```json
{
  "RateLimiting": {
    "GlobalPermitLimit": 100,
    "GlobalWindowMinutes": 1,
    "ApiPermitLimit": 60,
    "AuthPermitLimit": 10
  }
}
```

Production defaults (in `appsettings.Production.json`): global 200/min, API 120/min, auth 5/min.

### Key Environment Variables

| Variable | Config key | Notes |
|----------|-----------|-------|
| `ConnectionStrings__DefaultConnection` | Database connection string | Required |
| `Jwt__Secret` | JWT signing secret | Required in Production (≥ 32 chars) |
| `Database__Provider` | `SqlServer` \| `PostgreSQL` \| `Sqlite` | |
| `WebhookSettings__Secret` | MDM webhook HMAC key | Required if using MDM webhook |
| `Seq__ServerUrl` | Seq log shipping URL | Optional |
| `ASPNETCORE_ENVIRONMENT` | `Production` / `Development` | |

---

## Database Setup & Migrations

### Local (SQL Server LocalDB)

```bash
# Apply all pending migrations
dotnet ef database update \
  --project EnterpriseDataManager.Data \
  --startup-project EnterpriseDataManager

# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project EnterpriseDataManager.Data \
  --startup-project EnterpriseDataManager
```

### Docker / PostgreSQL (via migration container)

The `migrate` Docker Compose service runs `dotnet ef database update` using a dedicated SDK-based image:

```bash
docker compose run --rm migrate
```

This is separate from the runtime `web` container, which uses a lean runtime-only image.

### Current Migrations

| Migration | Description |
|-----------|-------------|
| `20260416163415_InitialDB` | Core schema: ArchivePlans, ArchiveJobs, ArchiveItems, RecoveryJobs, RetentionPolicies, StorageProviders, AuditRecords |
| `20260421183337_AddMfaStateTable` | MFA session persistence (`MfaStates`) |
| `20260421184225_AddScheduledJobRecords` | Scheduler persistence (`ScheduledJobRecords`) |
| `20260421185135_AddFilePermissionsAndIsSimulation` | ACL table (`FilePermissions`) and `IsSimulation` flag on jobs |

> The application checks for pending migrations at startup. In Production it throws and refuses to start if any are detected.

---

## Running Tests

```bash
# Unit tests
dotnet test EnterpriseDataManager.UnitTests/EnterpriseDataManager.UnitTests.csproj

# Integration tests (spins up SQLite in-process; PostgreSQL tests require Docker)
dotnet test EnterpriseDataManager.IntegrationTests/EnterpriseDataManager.IntegrationTests.csproj

# All tests with coverage
dotnet test EnterpriceDataManager.sln --collect:"XPlat Code Coverage"
```

### Integration Test Suites

| Suite | What it covers |
|-------|----------------|
| `Archive/ArchiveRecoverVerifyTests` | End-to-end archive → recover → SHA-256 verify flow |
| `Security/JwtSecurityTests` | `alg=none` bypass, tampered payload, expired token |
| `Security/InputValidationTests` | SQL injection, XSS, path traversal payloads — asserts no 500 |
| `RateLimiting/RateLimitingTests` | Global and auth limits, `Retry-After` header, IP partitioning |
| `Data/PostgreSqlMigrationTests` | Real PostgreSQL via Testcontainers — migrate, CRUD, idempotency |

---

## API Documentation

Swagger UI is available in **Development** at:

```
https://localhost:7112/api-docs
```

Disabled in Production. The raw OpenAPI JSON is at `/swagger/v1/swagger.json`.

### Authentication

Obtain a Bearer token:

```http
POST /api/v1/auth/token
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "YourPassword123!"
}
```

Response:

```json
{
  "token": "<jwt>",
  "refreshToken": "<opaque>",
  "expiresAt": "...",
  "expiresIn": 1800
}
```

Pass the JWT as `Authorization: Bearer <token>` on all subsequent requests.

### Token Refresh and Revocation

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{ "refreshToken": "<opaque>" }
```

```http
POST /api/v1/auth/logout        (requires Bearer token)
```

Logout revokes the refresh token by JTI; the short-lived access token expires naturally within 30 minutes.

### API Groups

| Group | Base path | Description |
|-------|-----------|-------------|
| Auth | `/api/v1/auth` | Token issue, refresh, logout |
| Archive Plans | `/api/v1/archive-plans` | CRUD + activate/deactivate |
| Archive Jobs | `/api/v1/archive-jobs` | List, start, cancel |
| Recovery Jobs | `/api/v1/recovery-jobs` | List, initiate, track |
| Retention Policies | `/api/v1/retention-policies` | CRUD |
| Storage Providers | `/api/v1/storage-providers` | CRUD + connection test |
| Audit Logs | `/api/v1/audit-logs` | Query with filters |
| Reports | `/api/v1/reports` | Excel + PDF exports |
| Mobile Backup | `/api/v1/mobile-backup` | Upload, MDM webhook |
| Dashboard | `/api/v1/dashboard` | Summary, stats, health, activity, storage usage |
| Share | `/api/v1/share` | Generate signed URLs, manage permissions |

---

## Deployment

### Option 1 — Automated runbook (recommended)

```bash
cp .env.example .env          # fill in all values
./scripts/deploy.sh
```

`deploy.sh` executes seven phases:

1. **Pre-flight** — validates `.env`, required variables, TLS certs, Docker availability
2. **Build** — `docker compose build --no-cache web`
3. **Migrate** — starts PostgreSQL, waits for `pg_isready`, runs `docker compose run --rm migrate`
4. **Start** — `docker compose up -d web nginx`
5. **Health** — polls `/health` up to 90 seconds
6. **Smoke tests** — runs `scripts/smoke-test.sh` against the live deployment
7. **Monitoring** — queries `/api/v1/dashboard/health` for system status

Flags: `--skip-build`, `--skip-smoke`.

### Option 2 — Manual Docker Compose

```bash
# Copy and fill in secrets
cp .env.example .env

docker compose up -d db
docker compose run --rm migrate
docker compose up -d web nginx
```

### Required `.env` values

| Variable | Description |
|----------|-------------|
| `DOMAIN` | Public hostname (e.g. `edm.example.com`) |
| `TLS_CERT_DIR` | Path to `fullchain.pem` + `privkey.pem` |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `JWT_SECRET` | JWT signing key (≥ 32 characters) |

See `.env.example` for the full reference including optional Seq, webhook, and notification settings.

### HashiCorp Vault (production secret management)

```bash
VAULT_ADDR=https://vault.example.com \
VAULT_TOKEN=<admin-token> \
  ./vault/setup-secrets.sh
```

This applies `vault/policy.hcl` (least-privilege read-only policy), writes all secrets to KV v2, and prints a periodic service token to inject into your deployment environment.

### Option 3 — Self-hosted (systemd)

```bash
dotnet publish EnterpriseDataManager -c Release -r linux-x64 \
  --self-contained true -o /opt/edm

# /etc/systemd/system/edm.service
[Unit]
Description=Enterprise Data Manager

[Service]
WorkingDirectory=/opt/edm
ExecStart=/opt/edm/EnterpriseDataManager
Restart=always
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Jwt__Secret=<secret>
Environment=ConnectionStrings__DefaultConnection=Host=...

[Install]
WantedBy=multi-user.target
```

```bash
systemctl enable edm && systemctl start edm
```

### CI/CD — GitHub Actions

`.github/workflows/ci.yml` runs on every push to `main`:

| Job | Steps |
|-----|-------|
| `build-and-test` | `dotnet build` + unit tests + integration tests (with Testcontainers) + coverage upload |
| `docker-security-scan` | Docker image build → **Trivy** CRITICAL/HIGH CVE scan → Trivy secret scan → SARIF upload to GitHub Security tab |

---

## Security

### Password Policy (Production)

| Setting | Value |
|---------|-------|
| Minimum length | 14 characters |
| Requires digit | Yes |
| Requires uppercase | Yes |
| Requires lowercase | Yes |
| Requires non-alphanumeric | Yes |
| Minimum unique chars | 6 |
| Lockout after | 3 failed attempts |
| Lockout duration | 30 minutes |

### HTTP Security Headers

| Header | Value |
|--------|-------|
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains; preload` |
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `X-XSS-Protection` | `1; mode=block` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | geolocation, microphone, camera, usb all denied |
| `Content-Security-Policy` | `default-src 'self'; frame-ancestors 'none'` (production) |

### Encryption

Sensitive archive data is encrypted at rest using **AES-256-GCM** via `IEncryptionService`. Signed share URLs are AES-encrypted and include an embedded expiry timestamp.

### MFA

TOTP-based MFA is compatible with Google Authenticator and any RFC 6238 authenticator app. Challenge state is persisted in the `MfaStates` database table — no in-memory loss on pod restarts.

---

## Project Structure

```
EnterpriseDataManager/                    ← git root
├── .github/workflows/ci.yml             ← GitHub Actions (build, test, Trivy scan)
├── EnterpriseDataManager/               ← solution root
│   ├── EnterpriceDataManager.sln
│   ├── docker-compose.yml               ← db, migrate, web, nginx, certbot
│   ├── Dockerfile                       ← multi-stage runtime image
│   ├── Dockerfile.migrate               ← SDK-based EF migration runner
│   ├── nginx.conf                       ← TLS 1.2/1.3, rate limiting, OCSP stapling
│   ├── .env.example                     ← secret catalogue with [REQUIRED]/[VAULT] notes
│   ├── scripts/
│   │   ├── deploy.sh                    ← 7-phase production deployment runbook
│   │   └── smoke-test.sh                ← curl-based API smoke test suite
│   ├── vault/
│   │   ├── policy.hcl                   ← least-privilege Vault policy
│   │   └── setup-secrets.sh             ← populates Vault KV v2, creates service token
│   ├── EnterpriseDataManager/           ← Web host project
│   │   ├── Areas/Identity/              ← Razor Identity pages (Login, Register, Manage)
│   │   ├── Controllers/
│   │   │   ├── Api/                     ← Versioned REST API controllers
│   │   │   └── MVC/                     ← MVC page controllers
│   │   ├── Filters/                     ← AuditActionFilter, RequireFilePermissionFilter
│   │   ├── Middleware/                  ← GlobalExceptionHandler, SecurityHeaders, AuditLogging
│   │   ├── Resources/                   ← .resx localisation files (en, bg)
│   │   ├── Views/                       ← Razor views
│   │   ├── wwwroot/                     ← Static assets
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   └── StartUp.cs
│   ├── EnterpriseDataManager.Core/      ← Domain entities and interfaces
│   ├── EnterpriseDataManager.Application/ ← MediatR + application services
│   ├── EnterpriseDataManager.Infrastructure/
│   │   ├── BackgroundJobs/             ← ArchivalJobScheduler, RetentionPolicyEnforcer, HealthCheckMonitor
│   │   ├── Identity/                   ← JWT, refresh tokens, LDAP, OIDC, TOTP, DbMfaStateStore
│   │   ├── Security/                   ← AES-256-GCM, ransomware protections
│   │   ├── Services/                   ← ReportService, ShareService, ArchivalService, etc.
│   │   └── Storage/                    ← LocalFilesystem, S3, Azure Blob, TapeDevice adapters
│   ├── EnterpriseDataManager.Data/     ← DbContext + EF migrations
│   ├── EnterpriseDataManager.Common/   ← Shared types, DTOs, pagination
│   ├── EnterpriseDataManager.WorkstationAgent/
│   ├── EnterpriseDataManager.UnitTests/
│   └── EnterpriseDataManager.IntegrationTests/
└── README.md
```

---

## License

Proprietary — all rights reserved.
