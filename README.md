# Prospera Assess backend

Layered ABP 10.7.0 / .NET 10 backend for uploading existing papers, approved memorandums and scripts, local AI marking, moderation and approval. Assessment authoring remains disabled. The proprietary model is awaiting training data; no trained marking model is deployed.

See [ABP_ARCHITECTURE.md](docs/ABP_ARCHITECTURE.md) for project boundaries, permissions, repositories, auditing, migration adoption and the retained authentication adapter.

```powershell
dotnet restore ProsperaAssess.sln
dotnet run --project backend/ProsperaAssess.DbMigrator
dotnet run --project backend/ProsperaAssess.HttpApi.Host
```

Configure ConnectionStrings:Default through .NET user secrets or ConnectionStrings__Default. The existing user-secret ID, SQL data, account hashes and client API routes are preserved. The host uses port 5080 by default.

For Visual Studio, reopen ProsperaAssess.sln and set **ProsperaAssess.HttpApi.Host** as the startup project. Its executable assembly name remains ProsperaAssess.Api.

Release deployments must run DbMigrator before the API. Debug/development can initialise locally; production defaults to verifying that migrations are current. The adoption baseline never drops installed user tables and is forward-only; take a verified SQL backup before first adoption.

Run SQL-backed tests with PROSPERA_TEST_SQL set to a local SQL Server connection string. They create and remove only their own ProsperaAssess_Test_* databases:

```powershell
dotnet test ProsperaAssess.sln
dotnet build ProsperaAssess.sln --configuration Release
```

The Dockerfile provides `api` and `dbmigrator` targets. The frontend workspace's Docker Compose uses a sibling checkout or explicit BACKEND_CONTEXT. The staging profile validates reviewed repository revisions and uses mounted secrets.

The local model service remains in ai-service. See [AI_MARKING.md](docs/AI_MARKING.md) for uploads, the memo-grounded file contract, language metadata, annotations and training-data preparation.

## Account security and staging

The host now supports password recovery/change, authenticator MFA and recovery codes, all-session revocation, protected refresh cookies and OpenAPI generation/validation. See [STAGING_AND_SECURITY.md](docs/STAGING_AND_SECURITY.md). Live hosting, DNS, licensed SQL and outgoing SMTP credentials must be configured separately.
