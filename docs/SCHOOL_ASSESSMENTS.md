# School assessment extension — v0.2

Prospera Assess remains one digital marking and moderation platform for schools, colleges and training providers.

## Implemented in prototype

- Optional school-specific assessment context on an assessment: subject, grade (1–12), term (1–4), assessment type.
- Input validation on grade/term and mandatory school metadata.
- `GET /api/schools/grades` and `GET /api/schools/assessments` for school-oriented clients.
- Existing rubric, submission, manual marking, moderation and approval flow works for school assessments.
- React School assessments workspace for setup and navigation.

## Not implemented (next milestones)

1. Identity/permissions: distinguish Teacher, HOD, Principal, district moderator, learners, and enforce role separation on backend (current actor fields are arbitrary text).
2. SQL Server and school/institution tenancy; data is currently in memory and resets at application restart.
3. Configurable official subject taxonomy, institution-specific CAPS/SBA weighting and school moderation rules, reviewed against current DBE requirements.
4. Supporting file upload, validated documents and original-script storage.
5. Moderation sampling workflow, independent moderation report and authorised mark release; existing moderation is basic approve/return only.
6. Separate learner identifiers and privacy controls suitable for children, plus POPIA safeguards.
7. Subject-specific AI training, evaluation and approval; no AI marking model has been trained.

**Do not deploy the demonstration API to production or use real school learner data.**
