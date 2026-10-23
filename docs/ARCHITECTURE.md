# Prospera Assess architecture — initial development

- ASP.NET Core 9 API: assessment, rubric, submissions, human marking, moderation and approval.
- Python FastAPI: separate proprietary-model research and inference service (currently 503, no model).
- Frontend: separate React repository.

## Limitations / security gates
This API is **developer demonstration only**: in-memory data, no real authentication, no persistent audit store, no file uploads, no multi-tenancy. Do not deploy publicly or use real learner data. Add ABP identity, SQL Server/EF Core migrations, storage and tests before production.

Workflow: Submitted -> Marked -> Moderated -> Approved; a moderator may return a script for remarking. Only manually reviewed marks can be approved.
