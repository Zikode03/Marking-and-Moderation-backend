# Prospera Assess — ABP Framework integration

## Phase 1: Host integration (committed)

- ABP Framework 9.3.6 selected for .NET 9 compatibility.
- Root ABP module: `ProsperaAssessModule`, depending on ASP.NET Core, Autofac and Entity Framework Core ABP infrastructure.
- ASP.NET Core starts ABP through `AddApplicationAsync<ProsperaAssessModule>()` and `InitializeApplicationAsync()`.
- Existing assessment, marking, moderation and approval REST routes are preserved.
- Existing ASP.NET Core Identity + Entity Framework Core SQL Server code is retained for compatibility while the full migration is designed.

## What this **does not** mean

It is not yet an ABP solution template with the built-in ABP Identity, Permission Management, Tenant Management, Audit Logging or OpenIddict modules. Current accounts remain ASP.NET Core Identity users, school scoping is manually implemented, and audit entries are custom JSON data. No ABP application services, repositories or permissions have replaced them.

## Remaining migration tasks

1. Create a migration-compatible EF Core schema strategy and backups; avoid `EnsureCreated()` in production.
2. Decide how to migrate current `AppUser` accounts, school IDs, roles and password hashes to ABP Identity without losing account access.
3. Add ABP Identity and Permission Management, seed permissions for Teacher, HOD, Principal and SchoolAdmin and enforce them in application services.
4. Introduce ABP multi-tenancy only after a mapped institution-tenant strategy has been defined and tested for horizontal privilege escalation.
5. Migrate assessment and submission operations into ABP application services and repositories, then enable audit logging.
6. Configure OpenIddict, token refresh and React integration.
7. Run clean restore/build, integration tests and security tests before any real learner records or public hosting.

### Local verification

```powershell
cd backend/ProsperaAssess.Api
dotnet restore
dotnet build
dotnet run
```

The application requires the local SQL Server and ASP.NET Core Identity bootstrap configuration described in `docs/SQL_AUTH_SETUP.md`. This repository has not yet had a verified ABP restore/build/runtime test. Do not treat this source-code integration as production-ready.
