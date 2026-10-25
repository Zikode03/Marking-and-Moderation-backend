# Prospera Assess backend

Layered ABP 9.3.6 / .NET 9 backend for uploading existing papers, approved memorandums and scripts, local AI marking, moderation and approval. Assessment authoring remains disabled. The proprietary model is awaiting training data; no trained marking model is deployed.

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

The Dockerfile provides `api` and `dbmigrator` targets. The frontend workspace's Docker Compose uses the mirrored backend sources under .local/backend.

The local model service remains in ai-service. See [AI_MARKING.md](docs/AI_MARKING.md) for uploads, the memo-grounded file contract, language metadata, annotations and training-data preparation.
