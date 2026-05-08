from fastapi import APIRouter

from app.providers.rule_based import RuleBasedProvider
from app.schemas import PredictRequest, PredictResponse

router = APIRouter()


@router.post("/match", response_model=PredictResponse)
async def match(req: PredictRequest) -> PredictResponse:
    """
    Staff matcher. Real ML implementation lands in chunk 9 (CLAUDE.md AI
    pipeline stage 6). For now, returns a null staff_user_id with a
    department-key-based recommendation; the .NET handler resolves the actual
    on-shift staff member from the DB. No LLM call here.
    """
    output = {
        "staff_user_id": None,
        "department_key": req.department_key,
        "recommendation": "auto-assign first available staff in department",
        "alternatives": [],
    }
    return PredictResponse(
        output=output,
        explanation=(
            "Rule-based matcher: AI service does not have staff-roster "
            "context; .NET caller selects the on-shift staff member."
        ),
        confidence=0.5,
        provider="rule_based",
        model_version=RuleBasedProvider.model_version,
        prompt_version=None,
        latency_ms=0,
        cost_usd=0.0,
        ai_fallback=False,
        fallback_reason=None,
    )
