# Backend repair — 23 October 2026

Scope: existing ASP.NET Core API, SQL persistence, Identity, separate Python research service, build/debug setup and documentation. Existing learner/assessment data is not migrated or deleted by these changes.

## Corrections

- Debug launches can load the configured user secrets even if Visual Studio omits the Development profile. Host URLs now honour configuration and command-line overrides.
- Added a root solution with the API and regression tests, and ignored Visual Studio caches.
- Extracted authentication, assessments, submissions, startup provisioning and validation from Program.cs.
- Login uses the Identity sign-in workflow, including lockout and two-factor enforcement. Added refresh-token renewal with expiry, security-stamp and lockout validation, without exposing public registration.
- Added per-IP login/refresh throttling (20 requests/minute) and configurable frontend CORS origins.
- Added null-safe validation, bounded inputs, explicit moderation decisions, and safe error responses for bad JSON and concurrency conflicts.
- Staff creation and role assignment run in one transaction; bootstrap settings and role-creation results are validated.
- School scoping applies to reads, writes and the AI placeholder. Created assessments and submissions now have working detail URLs.
- Moderated/approved scripts cannot be overwritten by marking. Returns for remarking preserve the review trail.
- New audit entries include marks, feedback and resulting status. Historical entries remain readable; missing historical evidence cannot be reconstructed.
- Submission updates use AuditJson as an EF concurrency token, so simultaneous workflow saves cannot silently overwrite each other. Mark/moderation requests optionally accept `revision` to detect stale client screens. Existing frontend requests remain compatible, but need to send revision to obtain that additional protection.
- Health checks test actual SQL connectivity.
- Release production startup no longer automatically creates schema unless explicitly configured. Development still supports fresh local databases.
- Python validation rejects blank, oversized and unexpected fields. The untrained model still returns 503.

## Verification

The API regression suite uses real SQL Server with a new uniquely named `ProsperaAssess_Test_*` database per run, then drops only that generated database. It covers authentication/renewal, permissions, school isolation, input failures, complete marking-to-approval flow, returns, self-moderation, immutable reviewed results, evidence snapshots, stale revisions, SQL concurrency and staff provisioning. The Python suite covers input validation and refusal to produce untrained marks.

## Remaining product work

This repair does not implement school onboarding, learner/class registers, document uploads, report cards, password recovery/email delivery, a full ABP Identity/OpenIddict migration, or a trained AI model. Pagination and frontend session renewal remain future work. Existing databases still use an EnsureCreated-era schema: an EF migration baseline, reviewed adoption plan and backup/restore checks are needed before production schema upgrades. Do not run EnsureCreated as a schema-upgrade mechanism. No production-readiness claim is made.

Verified results: 18 API integration tests passed in Debug and 18 in Release; 7 Python tests passed. NuGet advisory scan reported no vulnerable API packages in the configured sources. Python TestClient emitted a dependency deprecation warning for its httpx compatibility layer; all tests passed. No application database records were changed by the test suite.
