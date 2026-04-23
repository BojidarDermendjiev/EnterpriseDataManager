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

EDM is designed around a Bulgarian enterprise compliance audit report. It covers the full data lifecycle:

- **Archive** — schedule and run archival jobs against configurable storage providers
- **Recover** — guided recovery wizard restores data from archive items
- **Retain** — policy engine enforces data retention rules with soft-delete and configurable grace periods
- **Audit** — every write action is logged to the audit trail with user, timestamp, and delta
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
│  ArchiveJob     │   │  JWT Token Service  │   │  RetentionPolicyEnf. │
│  ArchiveItem    │   │  LDAP Connector     │   │  HealthCheckMonitor  │
│  RecoveryJob    │   │  OIDC Connector     │   │  WorkstationAgent    │
│  RetentionPolicy│   │  TOTP MFA Provider  │   └──────────────────────┘
│  StorageProvider│   │  Serilog + File     │
│  AuditRecord    │   │  NPOI (Excel)       │
│  FilePermission │   │  PdfSharpCore (PDF) │
└─────────────────┘   └─────────────────────┘
                                    │
                                    ▼
                     ┌─────────────────────────┐
                     │  Data Layer (EF Core 8)  │
                     │  SQL Server / PostgreSQL  │
                     │  / SQLite               │
                     └─────────────────────────┘
```

### Projects

| Project | Role |
|---------|------|
| `EnterpriseDataManager` | ASP.NET Core 8 web host — MVC + API controllers, Razor views, middleware |
| `EnterpriseDataManager.Core` | Domain entities, interfaces, Guard clauses (no external dependencies) |
| `EnterpriseDataManager.Application` | MediatR handlers, services, pipeline behaviours |
| `EnterpriseDataManager.Infrastructure` | EF Core, encryption, identity connectors, storage providers, background jobs |
| `EnterpriseDataManager.Data` | `DbContext`, EF migrations |
| `EnterpriseDataManager.Common` | Shared DTOs, result types, pagination helpers |
| `EnterpriseDataManager.WorkstationAgent` | .NET Worker Service — monitors and syncs workstations |
| `EnterpriseDataManager.UnitTests` | xUnit unit tests |
| `EnterpriseDataManager.IntegrationTests` | xUnit integration tests with in-memory or test DB |

---

## Features

### Archive Management
- Create and schedule **Archive Plans** with Cronos cron expressions
- Monitor **Archive Jobs** in real time (Pending → Running → Completed / Failed)
- Exponential back-off retry (up to 3 attempts, configurable)
- Job persistence via `ScheduledJobRecords` table — survives restarts

### Recovery
- Guided **Recovery Wizard** UI (step-by-step)
- Track **Recovery Jobs** with status, source archive, and audit trail
- Mobile backup REST endpoints (`/api/v1/mobile-backup/*`)
- Intune MDM webhook handler for mobile device backups

### Retention Policies
- Define policies by name, retention days, and trigger conditions
- **Retention Policy Enforcer** background service runs on a configurable interval
- Soft-delete with grace period before permanent removal
- Batch processing to avoid lock contention

### Storage Providers
- **Local filesystem** — direct path, ACL-aware
- **S3-compatible** — any S3 endpoint (AWS, MinIO, Wasabi)
- **Azure Blob Storage** — via Azure SDK
- **Tape Device** — simulation adapter (Windows-only, guarded)
- Provider CRUD UI with connection testing

### Audit & Compliance
- Every create/update/delete is written to `AuditRecords`
- Audit Log UI with date range filter and CSV/PDF export
- `AuditActionFilter` and `AuditLoggingMiddleware` for automatic capture
- File permissions stored in `FilePermissions` table

### Reporting
- **Excel export** (NPOI HSSFWorkbook) — archive jobs, recovery jobs, audit events
- **PDF export** (PdfSharpCore) — same datasets as printable reports
- Date range selector on the Reports page

### Security
- ASP.NET Core Identity with configurable password policy
- **JWT Bearer** for API clients; cookie auth for the MVC UI
- **TOTP MFA** (Google Authenticator compatible)
- **LDAP** and **OIDC** connectors for enterprise IdP integration
- **AES-256-GCM** encryption service for sensitive data at rest
- **HSTS**, **CSP**, **X-Frame-Options**, **X-Content-Type-Options** headers
- Global and per-endpoint **rate limiting** (fixed window)
- Account lockout after 5 failed attempts (15-minute window)

### Observability
- **Serilog** structured logging — console + rolling daily log files (30-day retention)
- **Health check** endpoints: `/health`, `/healthz`
- **HealthCheckMonitor** background service alerts on consecutive failures
- Notification hooks (email/webhook, configurable)

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
| Logging | Serilog 8 with file and console sinks |
| API Docs | Swashbuckle / Swagger UI, Asp.Versioning 8 |
| CI/CD | GitHub Actions (build → CodeQL SAST → Docker → GHCR) |
| Containers | Docker multi-stage (linux-x64), docker-compose, nginx |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (for local dev) **or** Docker (for PostgreSQL)
- [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`
- Docker + Docker Compose (for containerised deployment)

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

This creates the `EnterpriseDataManager` SQL Server LocalDB database with all 6 migrations applied.

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

Register at `/Identity/Account/Register`. The first user can be promoted to Administrator via the database or a seed script.

---

## Configuration Reference

All settings live in `appsettings.json` (and `appsettings.Production.json` for overrides). Sensitive values should be supplied as **environment variables** in production.

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

For PostgreSQL set the connection string to:
```
Host=localhost;Port=5432;Database=edm;Username=edm_user;Password=yourpassword
```

### JWT (API authentication)

```json
{
  "Jwt": {
    "Secret": "CHANGE-THIS-TO-A-32-CHAR-SECRET-IN-PROD",
    "Issuer": "EnterpriseDataManager",
    "Audience": "EnterpriseDataManager",
    "ExpiryMinutes": 60
  }
}
```

> **Never** commit the JWT secret. Pass it via the `Jwt__Secret` environment variable in production.

### CORS

```json
{
  "Cors": {
    "AllowedOrigins": ["https://localhost:5001", "https://yourdomain.com"]
  }
}
```

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
    "SoftDeleteGracePeriod": "30.00:00:00"
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

### Environment Variable Overrides (Production)

| Variable | Config key |
|----------|-----------|
| `ConnectionStrings__DefaultConnection` | Database connection string |
| `Jwt__Secret` | JWT signing secret (≥ 32 characters) |
| `Database__Provider` | `SqlServer` \| `PostgreSQL` \| `Sqlite` |
| `ASPNETCORE_ENVIRONMENT` | `Production` / `Development` |

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

### Docker / PostgreSQL

```bash
# Run migrations inside the container
docker compose run --rm web dotnet ef database update \
  --project EnterpriseDataManager.Data \
  --startup-project EnterpriseDataManager
```

### Current Migrations

| Migration | Description |
|-----------|-------------|
| `20260416163415_InitialDB` | Core schema: ArchivePlans, ArchiveJobs, ArchiveItems, RecoveryJobs, RetentionPolicies, StorageProviders, AuditRecords |
| `20260421183337_AddMfaStateTable` | MFA session persistence (`MfaStates`) |
| `20260421184225_AddScheduledJobRecords` | Scheduler persistence (`ScheduledJobRecords`) |
| `20260421185135_AddFilePermissionsAndIsSimulation` | ACL table (`FilePermissions`) and `IsSimulation` flag on jobs |

---

## Running Tests

```bash
# Unit tests
dotnet test EnterpriseDataManager.UnitTests/EnterpriseDataManager.UnitTests.csproj

# Integration tests
dotnet test EnterpriseDataManager.IntegrationTests/EnterpriseDataManager.IntegrationTests.csproj

# All tests with coverage
dotnet test EnterpriceDataManager.sln --collect:"XPlat Code Coverage"
```

---

## API Documentation

When running in **Development** mode, Swagger UI is available at:

```
https://localhost:7112/api-docs
```

The raw OpenAPI JSON is at `/swagger/v1/swagger.json`.

### Authentication

API endpoints require a **Bearer token**. To obtain one:

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "YourPassword123!"
}
```

Response includes `token` — pass it as `Authorization: Bearer <token>` on subsequent requests.

### API Versioning

All API routes are versioned under `/api/v{version}/`. The current stable version is **v1**. The version can also be specified via the `X-Api-Version` request header.

### Key API Groups

| Group | Base path | Description |
|-------|-----------|-------------|
| Auth | `/api/v1/auth` | Login, token refresh |
| Archive Plans | `/api/v1/archive-plans` | CRUD for archive plans |
| Archive Jobs | `/api/v1/archive-jobs` | List, start, cancel jobs |
| Recovery Jobs | `/api/v1/recovery-jobs` | List, initiate recovery |
| Retention Policies | `/api/v1/retention-policies` | CRUD for policies |
| Storage Providers | `/api/v1/storage-providers` | CRUD + connection test |
| Audit Logs | `/api/v1/audit-logs` | Query audit records |
| Reports | `/api/v1/reports` | Excel and PDF exports |
| Mobile Backup | `/api/v1/mobile-backup` | Mobile device backup endpoints |
| Dashboard | `/api/v1/dashboard` | Aggregated statistics |
| Share | `/api/v1/share` | Generate signed share URLs |

---

## Deployment

### Option 1 — Docker Compose (recommended)

```bash
cd EnterpriseDataManager

# Set secrets
export POSTGRES_PASSWORD=yourStrongPassword
export JWT_SECRET=your-32-char-minimum-secret-here

docker compose up -d
```

Services started:
- **db** — PostgreSQL 16 on port 5432
- **web** — EDM application on port 8080
- **nginx** — reverse proxy on ports 80 / 443

Apply migrations after first start:
```bash
docker compose run --rm web dotnet ef database update \
  --project EnterpriseDataManager.Data \
  --startup-project EnterpriseDataManager
```

#### TLS in Production

Mount your certificate into nginx and update `nginx.conf`:

```nginx
server {
    listen 443 ssl;
    ssl_certificate     /etc/nginx/certs/fullchain.pem;
    ssl_certificate_key /etc/nginx/certs/privkey.pem;
    ...
}
```

### Option 2 — Self-Hosted (existing Windows Server)

```powershell
# Publish a self-contained binary
dotnet publish EnterpriseDataManager -c Release -r win-x64 --self-contained true -o ./publish

# Install as a Windows Service
New-Service -Name "EDM" -BinaryPathName "C:\edm\EnterpriseDataManager.exe"
Start-Service EDM
```

Set connection string and JWT secret via Windows environment variables or a `appsettings.Production.json` file next to the executable.

### Option 3 — Self-Hosted (Linux / systemd)

```bash
dotnet publish EnterpriseDataManager -c Release -r linux-x64 --self-contained true -o /opt/edm

# /etc/systemd/system/edm.service
[Unit]
Description=Enterprise Data Manager

[Service]
WorkingDirectory=/opt/edm
ExecStart=/opt/edm/EnterpriseDataManager
Restart=always
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Jwt__Secret=your-secret
Environment=ConnectionStrings__DefaultConnection=Host=...

[Install]
WantedBy=multi-user.target
```

```bash
systemctl enable edm && systemctl start edm
```

### CI/CD — GitHub Actions

The included `.github/workflows/ci-cd.yml` pipeline runs on every push to `main` or `develop`:

1. **build-and-test** — `dotnet build` + unit tests + integration tests
2. **sast** — GitHub CodeQL static analysis (C#)
3. **docker** — builds and pushes a Docker image to GHCR (`ghcr.io/<owner>/edm-web:latest`) on `main` only

Required GitHub secret: none (uses `GITHUB_TOKEN` for GHCR push).

---

## Security

### Password Policy

| Setting | Value |
|---------|-------|
| Minimum length | 12 characters |
| Requires digit | Yes |
| Requires uppercase | Yes |
| Requires lowercase | Yes |
| Requires non-alphanumeric | Yes |
| Minimum unique chars | 4 |
| Lockout after | 5 failed attempts |
| Lockout duration | 15 minutes |

### HTTP Security Headers

| Header | Value |
|--------|-------|
| `Strict-Transport-Security` | `max-age=31536000` (production only) |
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `X-XSS-Protection` | `1; mode=block` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | geolocation, microphone, camera, usb all denied |
| `Content-Security-Policy` | `default-src 'self'` (stricter in production) |

### Encryption

Sensitive archive data is encrypted at rest using **AES-256-GCM** via `IEncryptionService`. Signed share URLs are AES-encrypted and include an expiry timestamp.

### MFA

TOTP-based MFA is supported via `TotpMfaProvider` and is compatible with Google Authenticator and any RFC 6238 authenticator app. MFA session state is persisted in the `MfaStates` database table.

---

## Project Structure

```
EnterpriseDataManager/                   ← git root
├── .github/workflows/ci-cd.yml          ← GitHub Actions pipeline
├── EnterpriseDataManager/               ← solution root
│   ├── EnterpriceDataManager.sln
│   ├── docker-compose.yml
│   ├── Dockerfile
│   ├── nginx.conf
│   ├── EnterpriseDataManager/           ← Web host project
│   │   ├── Areas/Identity/              ← Razor Identity pages (Login, Register, Manage)
│   │   ├── Controllers/
│   │   │   ├── Api/                     ← Versioned REST API controllers
│   │   │   └── MVC/                     ← MVC page controllers
│   │   ├── Filters/                     ← AuditActionFilter, ValidateModelAttribute
│   │   ├── Middleware/                  ← GlobalExceptionHandler, SecurityHeaders, AuditLogging
│   │   ├── Resources/                   ← .resx localisation files (en, bg)
│   │   ├── Views/                       ← Razor views (Layout, per-feature folders)
│   │   ├── wwwroot/                     ← Static assets (CSS, JS, lib)
│   │   ├── appsettings.json
│   │   └── StartUp.cs
│   ├── EnterpriseDataManager.Core/      ← Domain entities and interfaces
│   │   └── Entities/
│   ├── EnterpriseDataManager.Application/ ← MediatR + application services
│   ├── EnterpriseDataManager.Infrastructure/
│   │   ├── BackgroundJobs/             ← ArchivalJobScheduler, RetentionPolicyEnforcer, HealthCheckMonitor
│   │   ├── Identity/                   ← JWT, LDAP, OIDC, TOTP
│   │   ├── Security/                   ← AES-256-GCM, ransomware protections
│   │   ├── Services/                   ← ReportService, ShareService, ArchivalService, etc.
│   │   └── Storage/                    ← LocalFilesystem, S3, Azure Blob, TapeDevice adapters
│   ├── EnterpriseDataManager.Data/     ← DbContext + EF migrations
│   ├── EnterpriseDataManager.Common/   ← Shared types, DTOs, pagination
│   ├── EnterpriseDataManager.WorkstationAgent/ ← Windows/Linux background worker
│   ├── EnterpriseDataManager.UnitTests/
│   └── EnterpriseDataManager.IntegrationTests/
└── README.md
```

---

## License

Proprietary — all rights reserved.
