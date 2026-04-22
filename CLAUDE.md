# EnterpriseDataManager — CLAUDE.md

## What this project is

Enterprise-grade data archival and storage management platform built on ASP.NET Core 8. It serves two mandatory systems:
1. **Archival System** — automated backup from servers/workstations/mobile, AES-256-GCM encryption, ransomware protection, retention policies, recovery with SHA-256 verification
2. **Storage Management System** — centralized access control (LDAP/OIDC/MFA), audit logging, file sharing and ACL

## Solution layout

```
EnterpriseDataManager/          ← root
├── EnterpriseDataManager/      ← ASP.NET Core MVC + API host
├── EnterpriseDataManager.Application/   ← CQRS via MediatR, FluentValidation, AutoMapper
├── EnterpriseDataManager.Core/          ← domain entities, interfaces, events, value objects
├── EnterpriseDataManager.Data/          ← EF Core, DbContext (SQL Server), repositories, UoW
├── EnterpriseDataManager.Infrastructure/← storage providers, security, identity, background jobs
├── EnterpriseDataManager.UnitTests/
└── EnterpriseDataManager.IntegrationTests/
```

## Technology stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 8, MVC + API Controllers |
| ORM | EF Core 8, SQL Server (target: PostgreSQL — see migration section) |
| CQRS | MediatR 12.2 with pipeline behaviors |
| Validation | FluentValidation 11.9 |
| Mapping | AutoMapper 13 |
| Auth | ASP.NET Identity + LDAP + OIDC + TOTP MFA |
| Encryption | AES-256-GCM with PBKDF2 |
| Storage | Local FS, Azure Blob, S3-compatible |
| Background | Custom BackgroundService + Cronos scheduling |
| Logging | Serilog (sinks not yet configured) |
| Email | MailKit |
| Ransomware | AnomalyDetector, WormSimulator, ImmutableStorageService |

## Critical stubs — DO NOT ship as-is

These files contain fake/simulated logic that must be replaced before production:

| File | Stub location | What it fakes |
|---|---|---|
| `Infrastructure/BackgroundJobs/ArchivalJobScheduler.cs` | `RunArchiveJobAsync`, `RunRestoreJobAsync`, etc. | `Task.Delay(1s)` + hardcoded metrics |
| `Infrastructure/BackgroundJobs/RetentionPolicyEnforcer.cs` | `GetAffectedItemsAsync`, `DeleteItemAsync`, `SoftDeleteItemAsync`, `ArchiveItemAsync` | Hardcoded single item, no-op operations |
| `Infrastructure/BackgroundJobs/HealthCheckMonitor.cs` | `CheckDatabaseAsync`, `CheckStorageAsync`, `CheckCacheAsync`, `CheckNetworkAsync` | Always returns `true` with fake data |
| `Infrastructure/Identity/InMemoryMfaStateStore` | entire class | MFA sessions lost on app restart |

## Architecture patterns

- **Repository + Unit of Work** — `IGenericRepository<T>`, `IUnitOfWork` in Data layer
- **Domain Events** — dispatched via `DomainEventDispatchInterceptor` during `SaveChangesAsync`
- **Audit Interceptor** — `AuditSaveChangesInterceptor` auto-sets `CreatedBy/At`, `UpdatedBy/At`
- **Soft Delete** — global EF query filter on `ISoftDeletable`
- **MediatR Pipeline** — `LoggingBehavior` → `ValidationBehavior` → `UnhandledExceptionBehavior`
- **ProblemDetails** — `ExceptionHandlingMiddleware` returns RFC 7807 with `correlationId`

## API surface

All API controllers require `[Authorize]` and live under `/api/`:
- `ArchiveJobsApiController`, `ArchivePlansApiController`, `AuditLogsApiController`
- `DashboardApiController`, `RecoveryJobsApiController`, `RetentionPoliciesApiController`
- `StorageProvidersApiController`

MVC controllers (Razor views, localized BG/EN): `ArchiveJobs`, `ArchivePlans`, `AuditLogs`, `RecoveryJobs`, `Reports`, `RetentionPolicies`, `Settings`

Swagger/OpenAPI available at `/api-docs` (dev only). Bearer scheme documented but JWT not yet wired in DI.

## Security configuration (StartUp.cs)

- HSTS 1 year, X-Frame-Options DENY, X-Content-Type-Options nosniff, Referrer-Policy strict-origin
- CSP: strict in prod, relaxed in dev
- Rate limiting: 100 req/min global (per user or IP), 60/min API, 10/min auth
- Password: min 12 chars, digit, upper, lower, non-alphanumeric, 4 unique
- Lockout: 5 attempts → 15 min
- CORS: explicit origin list from config, `AllowCredentials`

## Database

Currently **SQL Server** (`Microsoft.EntityFrameworkCore.SqlServer`). Connection string in `appsettings.json`:
```
Server=.;Database=EnterPriceDataManager;Trusted_Connection=True;TrustServerCertificate=True
```

**Target**: migrate to PostgreSQL (Npgsql) — see migration plan in main PLAN.md.

One migration exists: `20260416163415_InitialDB`.

## Missing features (from spec)

1. Workstation agent (Windows Service) for automatic backup
2. Mobile backup endpoint (REST / MDM Intune webhook)
3. Test recovery wizard (simulation mode, no prod overwrite)
4. ACL/file permissions (`FilePermission` entity, `IShareService`)
5. File sharing (signed URLs with permission scope)
6. Real dashboard metrics (DashboardApiController returns scaffold)
7. Reports module (PDF/Excel export via FastReport or NPOI)
8. Docker Compose / Dockerfile (not present in repo)
9. CI/CD pipeline (GitHub Actions)
10. Serilog sinks (file, cloud) — currently console only

## Running locally

```bash
# Restore and build
dotnet restore EnterpriseDataManager/EnterPriceDataManager.sln
dotnet build

# Apply migrations
cd EnterpriseDataManager.Data
dotnet ef database update --startup-project ../EnterpriseDataManager

# Run
cd ../EnterpriseDataManager
dotnet run
```

Swagger UI: https://localhost:7001/api-docs

## Key conventions

- Controllers inherit `ApiBaseController` (API) or `Controller` (MVC) — use `Success()`, `NotFoundResponse()`, `BadRequestResponse()` helpers
- Commands/Queries live in `Application/Commands` and `Application/Queries`, handlers in `Application/Handlers`
- New entities must implement `BaseEntity` and optionally `IAuditable`, `ISoftDeletable`
- All new API endpoints must have `[Authorize]` and be covered by `ValidateModelAttribute`
- FluentValidation validators required for all commands
- No comments unless the WHY is non-obvious
