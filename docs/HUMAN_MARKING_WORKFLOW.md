# Academic marking and moderation — AI-first workflow

## Intended current product process

1. **Upload:** authorised teaching staff upload an existing question paper, approved memorandum and complete learner scripts.
2. **AI marking:** once an independently approved local model is connected, processing produces provisional question-level scores, evidence, highlights and suggestions for improvement.
3. **Teacher/lecturer review (required):** the allocated teaching staff member reads the original work and memorandum alongside AI scores/annotations, confirms or corrects each question mark, and records a reason including any changes to learner improvement feedback. This transitions scripts to `TeacherReviewed`.
4. **HOD/academic moderation (required):** a different authorised reviewer inspects the reviewed result, corrections and evidence. The moderator either confirms it (`Moderated`) or returns it with a reason (`ReturnedToTeacher`).
5. **Correction loop:** returned work goes back to teaching staff for another review. It cannot skip to moderation or final approval.
6. **Final approval:** principal or authorised academic management approves a moderated result (`Approved`). Only approved results are exported for reporting.

## Current API

- `POST /api/ai/batches/{batchId}/scripts/{scriptId}/review` — teacher/lecturer confirms or corrects AI marks (ABP Mark permission).
- `POST /api/ai/batches/{batchId}/scripts/{scriptId}/moderate` — HOD/academic moderator confirms (ABP Moderate permission).
- `POST /api/ai/batches/{batchId}/scripts/{scriptId}/return` — moderator returns to teacher with reason (ABP Moderate permission).
- `POST /api/ai/batches/{batchId}/scripts/{scriptId}/approve` — final approval (ABP Approve permission).

Teacher review may include a `feedback` dictionary keyed by question number. When supplied, it must include every question, with at most 5000 characters per answer. Corrections become the current learner feedback; the audit retains the preceding AI result. Older clients that omit feedback keep the existing feedback.

Each POST uses the script's optimistic-concurrency revision and existing `AiReview` contract (reason plus confirmed marks for review/moderation). Server-side identity, academic allocations and separation-of-duty checks apply. The audit records capture the preceding score snapshot.

## Status mapping

`Uploaded → Queued → Processing → AwaitingModeration/NeedsAttention → TeacherReviewed → Moderated → Approved`.

Existing `AwaitingModeration` status is retained for stored-data compatibility but **displayed as Awaiting teacher review**; its database name is legacy. If returned from HOD, `TeacherReviewed → ReturnedToTeacher → TeacherReviewed`. The new statuses are stored as strings in the existing column, so no schema change is required.

## Important scope boundary

**No AI model has been trained or deployed yet.** The normal application does not create fake academic AI marks. Scripts can be uploaded and retained, but teacher-review actions are not enabled until a real model result exists. Research UI fixtures/test data are fictional and never released as results.

The older text-submission `/api/submissions` workflow remains as a compatibility adapter but is not the product's current marking workflow. No separate human-first PDF grading path is being implemented.

## Verification

Source changes and regression tests are committed, but a full clean frontend/backend test run and database-backed end-to-end walkthrough have not yet been verified. Do not use real learners or deploy publicly before those checks and appropriate security assurance.
