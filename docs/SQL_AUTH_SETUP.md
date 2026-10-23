# SQL Server and Identity developer setup

This replaces the in-memory prototype, but **is not a finished production security architecture**.

## Configure locally

Use .NET 9 SDK and a SQL Server instance. Do not commit credentials.

```powershell
cd backend/ProsperaAssess.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=ProsperaAssess;User Id=sa;Password=YOUR_LOCAL_PASSWORD;TrustServerCertificate=True"
dotnet user-secrets set "Bootstrap:Email" "school-admin@example.test"
dotnet user-secrets set "Bootstrap:Password" "CHANGE_TO_A_LONG_UNIQUE_LOCAL_PASSWORD"
dotnet user-secrets set "Bootstrap:SchoolId" "81b1cd8f-602a-4e22-8cca-e8d403cd874f"
dotnet run
```

First boot creates database tables and optionally creates an initial SchoolAdmin (one institution only). Set a unique SchoolId for each installation; multi-school onboarding/invitations are **not implemented**. To authenticate, `POST /login?useCookies=false` with `{"email":"school-admin@example.test","password":"..."}`, then send `Authorization: Bearer TOKEN`. Tokens are short-lived and handled by ASP.NET Core Identity.

No public registration endpoint is mapped. The bootstrap password must not be committed or publicly exposed. Staff account provisioning, SQL migrations, comprehensive tenant security testing, reset flows, malware scanning, rate limits, HTTPS and ABP integration remain to be implemented.

**Development restrictions:** keep the backend bound to localhost; never upload real student records. The AI model remains untrained.
