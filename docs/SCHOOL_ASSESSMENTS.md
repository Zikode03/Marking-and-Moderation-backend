# Grades 8–12 school support — implementation update

The shared Prospera Assess product can now represent private/independent and public high-school assessments.

## Current development prototype
- Assessment metadata: school type, IEB / DBE-NSC / Other pathway, grade 8–12, school term 1–4, subject, assessment type and optional assessment weighting (0–100%).
- Rubric-based human marking, basic moderation and approval remain available for those assessments.
- Simple moderation separation rule: entered moderator name cannot equal the last entered marker name.
- School summary API: `GET /api/schools/summary` gives assessment/submission/moderation counts and average approved percentage.
- Both assessments and grades remain in process memory and reset on restart.

## Critical limitations
- The weight field is **metadata only**. No official IEB/DBE CAPS SBA or promotion formula is implemented or certified.
- Marker/moderator names are self-entered; identity and roles are not authenticated. This is **not a security boundary**.
- There is no authorised school tenant separation, child-data privacy system, SQL persistence, official report card generation or document scanning.
- A trained proprietary Prospera AI marking model does not yet exist.
- Use fictional development data only. Do not deploy this API publicly.

## Required next release
1. Add ABP authentication, institution isolation and school roles (Teacher, HOD, Principal, School Admin).
2. Use SQL Server persistence and migrations with durable marking histories.
3. Define formally reviewed IEB/DBE institution-specific SBA schemes; calculate only validated weighting structures.
4. Implement detailed moderation sampling, evidence, approvals and revision history.
5. Implement document upload/storage and student privacy/security controls.
6. Build and separately validate the Python/PyTorch marking model.
