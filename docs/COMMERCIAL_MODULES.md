# Institution and operational modules

## Migration

`20261025205747_InstitutionModules` adds `InstitutionRecords` and `InstitutionEvents` with institution/code uniqueness, revision concurrency checks and audit indexes. It does not replace legacy account, assessment or script records. Run `dotnet run --project backend/ProsperaAssess.DbMigrator` with the intended environment connection settings before deploying. Test migrations on a backup or staging copy before updating an existing deployment.

Structured module data uses field whitelists, type/range validation, institution-scoped reference validation and archive dependency checks in `InstitutionAppService`. Stable record IDs preserve enrolments when descriptive names change. Candidate references cannot be renamed after registration. No public institution-registration endpoint is enabled; registration configures the institution assigned to the account.

## API

- `/api/institution/records/{kind}`: read/create records; update by `/{id}` with the current revision. Kinds include institution, branch, subject, qualification, module, class, learner, enrolment, session, assessment, settings and integration.
- `/api/institution/import/{kind}`: atomically create 1-500 records. Invalid rows roll back the full import.
- `/api/institution/staff`: roster; PUT `/{id}` changes active state and roles. School administrators cannot modify their own access.
- `/api/institution/invitations`: create a single-use, seven-day activation link. Only a hash of the random invitation token is persisted. Public acceptance is rate-limited and excluded from audit parameter logging.
- `/api/institution/documents`: immutable original fingerprints, predecessor links and approval status; POST `/{batchId}/approve` requires an independent academic reviewer.
- `/api/institution/assignments/{scriptId}`: assign different, qualified teaching and moderation reviewers with an optional deadline. Assigned decision-makers are enforced by the review/moderation API.
- `/api/institution/samples/{percentage}`: record a reproducible inspection sample and population in audit history. Sampling never bypasses the existing approval chain.
- `/api/institution/tasks`: role/assignment-aware open work and deadlines.
- `/api/institution/results`: approved-only results with reviewed feedback and session/class context; optional candidate filter.
- `/api/institution/audit`: recent institution events and result-decision history with an optional action/actor/candidate search.

The new Moderator role is seeded once using a separate checkpoint, preserving revocations of existing role permissions. Disabled users, changed role claims and revoked security stamps are checked during account resolution. Refresh also validates the identity security stamp.

## Existing workflow extensions

Upload context accepts `previousBatchId` to retain a revision chain and school academic year. Policy can require active candidate enrolment for the actual grade/module and year, and independent approval of the exact memorandum version before marking. Duplicate script content in the same academic context is rejected unless the upload explicitly declares a document revision.

Teacher review accepts optional corrected `evidence` alongside per-question feedback and overall summary. Audit snapshots retain the earlier result. Marked training labels can record learner-only transcription, annotation-handling notes and dataset scope. Export includes that preparation metadata and a pseudonymised learner group; verified exports still require a second reviewer.

## Operational boundaries

The current release provides in-app task notifications and manually shared staff invitation links. No email/SMS provider, Microsoft/Moodle connector, public onboarding or billing service is configured. Integration registry records are requirements/contact metadata, not live connections. Retention days record institution policy and do not enable automatic file deletion. Backups and restoration must be operated and verified for the chosen deployment environment.

Server and browser regression checks use fictional data. Actual model training, evaluation and release approval still require representative academic data.
