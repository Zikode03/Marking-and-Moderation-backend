# Human marking workflow — correction 25 October 2026

## Current legacy text-submission workflow

Submitted → Marked → Moderated → Approved

On HOD rejection: Marked → ReturnedForRemarking → Marked → Moderated → Approved

The `ReturnedForRemarking` state is distinct from a never-marked submission. The marker may edit and resubmit it. The HOD must review again before approval; neither returned nor merely marked work can bypass moderation. Audit history retains the HOD's reason.

### Existing protections
- Server-controlled marker/moderator identity, ABP permissions and school/academic allocation checks
- Marker cannot moderate own work
- Rubric range and complete criterion validation
- Version/revision conflict checks
- Moderated and approved results locked from ordinary remarking

### Separate PDF-script workflow — outstanding design decision

The main modern workspace uses `AiScript` uploaded-paper batches, with statuses Uploaded, Queued, Processing, AwaitingModeration, NeedsAttention, Moderated, Approved. That workflow presently expects a model-produced result before moderation and **does not yet provide an independent human-only marking path from an uploaded PDF**.

To support operation before the model is ready, implement a separate manual initial marking action for uploaded scripts. That action must:
1. Accept staff-confirmed question definitions, maximum marks, marks and comments with independent reviewer identity.
2. Save a versioned human-authored result separate from an AI-generated suggestion.
3. Transition Uploaded → AwaitingModeration (human), then allow HOD return/revise and approval.
4. Enforce authorised grade/subject or faculty/module allocation, non-self-moderation and audit history.
5. Update the main React queues and review desk to support this mode without pretending a trained model exists.

**Current commit corrects the legacy text submission lifecycle only.** Do not treat the main uploaded-PDF workflow as fully repaired.
