# Grades 8–12 assessment support

Assessments can store school type, subject, grade 8–12, term 1–4, assessment type, IEB/DBE-NSC/Other pathway and a 0–100 percent planning weight. Metadata is validated and stored in SQL. It does not implement official SBA, promotion or report-card calculations.

School assessment routes: GET /api/schools/grades and GET /api/schools/assessments. All assessment/submission data is scoped to the authenticated account school. Teacher/AcademicAdmin can mark, HOD/AcademicAdmin can moderate, and Principal/SchoolAdmin/AcademicAdmin can approve. Moderation cannot be performed by the last marker. Self-entered actor names are ignored in favour of authenticated IDs.

Learner/class registers, school onboarding, document handling, moderation sampling and official reporting remain unimplemented. AI marking remains unavailable. See BACKEND_REVIEW.md for the current repair scope.
