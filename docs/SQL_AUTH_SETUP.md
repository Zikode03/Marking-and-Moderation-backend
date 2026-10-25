# SQL Server and Identity setup

The current API persists assessments, submissions, staff roles and marking evidence in SQL Server. It uses ASP.NET Core Identity behind an infrastructure adapter in the layered ABP application; separate ABP Account/Identity/OpenIddict module adoption remains an explicit future account migration.

## Local setup

From the repository root:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Server=127.0.0.1,14332;Database=ProsperaAssess;User Id=sa;Password=YOUR_LOCAL_PASSWORD;Encrypt=True;TrustServerCertificate=True" --project backend/ProsperaAssess.HttpApi.Host
dotnet run --project backend/ProsperaAssess.DbMigrator
dotnet run --project backend/ProsperaAssess.HttpApi.Host --launch-profile http
```

The project already has a UserSecretsId. Secrets stay outside Git. The `http` profile sets Development; Debug builds also load the saved SQL connection when launched without that profile. After changing project or startup settings, stop debugging, rebuild, then start again.

Existing staff accounts remain in SQL. To provision the first administrator on a new installation, configure all three `Bootstrap:Email`, `Bootstrap:Password` and `Bootstrap:SchoolId` settings in user secrets. SchoolId must be a non-empty GUID. An existing matching account is not reset or reassigned. A partial or inconsistent bootstrap configuration fails clearly. Bootstrap is not required when accounts already exist.

## Authentication and staff

`POST /login` accepts email/password and returns Identity bearer access and refresh tokens. `POST /refresh` accepts `{ "refreshToken": "..." }`; expired tokens, invalid security stamps and locked accounts are rejected. Public registration is not mapped. Login honours two-factor requirements, but the client does not yet implement a two-factor challenge screen. Login and refresh share a 20-request/minute per-IP rate limit.

Send `Authorization: Bearer TOKEN` to protected endpoints. A SchoolAdmin can call `POST /api/bootstrap/staff` with email, password and role (Teacher, Lecturer, Professor, HOD, Principal or AcademicAdmin). Teacher creation additionally requires grade (8–12) and subject; Lecturer/Professor creation requires moduleId for a module in the same tenant. The initial allocation is saved in the same transaction as the account and role. SchoolId always comes from the administrator. Role assignment is transactional. A SchoolAdmin is not automatically granted marking or moderation privileges.

## Runtime configuration

- `ConnectionStrings__Default`: SQL connection string for environment-based configuration.
- `ASPNETCORE_URLS`: host binding; defaults to localhost:5080 when unset.
- `Cors__Origins__0`: allowed frontend origin; defaults to http://localhost:5173.
- `Database__Initialize`: allow fresh schema creation with EnsureCreated. Defaults on for Development/Debug, off for Release production. For Docker Release development, set this explicitly to true only for a new local database. Existing databases can run with false.

The current schema has no versioned EF migration baseline. EnsureCreated does not update existing schema. Establish migrations and a reviewed baseline adoption/backup strategy before production upgrades. The app must use a dedicated database and a suitable application SQL account outside local development.

## Regression tests

Set `PROSPERA_TEST_SQL` using a local connection string; `dotnet test ProsperaAssess.sln` creates and drops only a generated `ProsperaAssess_Test_*` database. The normal ProsperaAssess database is not used by the tests.

## Academic structures upgrade

Existing local databases require the additive `academic-schema-v1.sql` upgrade. Development/Debug startup applies it when Database:Initialize is enabled. It adds only the faculty/module/allocation tables and the nullable institution assessment column. It does not reset users or rewrite existing marks. Back up existing databases first. See ACADEMIC_STRUCTURE.md for Grade 8–12 teaching and faculty/module access rules.
