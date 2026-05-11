-- =============================================================================
-- Migration 0003 — AI enrichment fields on issues
-- =============================================================================
-- Adds four columns that the AI pipeline background worker populates moments
-- after an issue is created. Staff see ai_category + ai_priority_score on issue
-- cards; full reasoning lives in ai_predictions (linked via issue_id).
--
-- Idempotent — safe to re-run. Uses ADD COLUMN IF NOT EXISTS / CREATE INDEX
-- IF NOT EXISTS, and gates CHECK constraints with a DO block that probes
-- pg_constraint. This protects local dev environments where the file may be
-- applied more than once during iteration.
--
-- Two-deploy pattern is NOT required: these are new nullable columns with safe
-- defaults — no existing code reads or writes them until the backend is
-- deployed with this migration applied.
-- =============================================================================

ALTER TABLE issues
    ADD COLUMN IF NOT EXISTS ai_category            TEXT,
    ADD COLUMN IF NOT EXISTS ai_category_confidence NUMERIC(5, 4),
    ADD COLUMN IF NOT EXISTS ai_priority_score      INTEGER,
    ADD COLUMN IF NOT EXISTS ai_fallback            BOOLEAN NOT NULL DEFAULT FALSE;

-- Constraints — add only if not already present. The auto-generated PostgreSQL
-- name for an inline CHECK on a column is "<table>_<column>_check", so existing
-- environments where 0003 was applied with inline CHECKs match these probes.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'issues_ai_category_confidence_check'
    ) THEN
        ALTER TABLE issues
            ADD CONSTRAINT issues_ai_category_confidence_check
            CHECK (ai_category_confidence IS NULL
                OR (ai_category_confidence >= 0 AND ai_category_confidence <= 1));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'issues_ai_priority_score_check'
    ) THEN
        ALTER TABLE issues
            ADD CONSTRAINT issues_ai_priority_score_check
            CHECK (ai_priority_score IS NULL
                OR (ai_priority_score >= 0 AND ai_priority_score <= 100));
    END IF;
END $$;

-- Used in analytics ("recurring problems by category") and staff filter UI.
CREATE INDEX IF NOT EXISTS idx_issues_ai_category
    ON issues (business_id, ai_category)
    WHERE ai_category IS NOT NULL;

-- =============================================================================
-- END OF MIGRATION 0003
-- =============================================================================
