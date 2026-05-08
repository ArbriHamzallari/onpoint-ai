from fastapi import APIRouter

from app.providers.rule_based import RuleBasedProvider
from app.schemas import PredictRequest, PredictResponse

router = APIRouter()


@router.post("/recommend", response_model=PredictResponse)
async def recommend(req: PredictRequest) -> PredictResponse:
    """
    Solution recommender. Full retrieval-over-resolved-issues lands in chunk
    9 (CLAUDE.md AI pipeline stage 7). For now returns an empty top-5; the
    response shape matches the future implementation so chunk 4c can wire
    the .NET caller and the staff dashboard renders gracefully.
    """
    output = {
        "top_solutions": [],
        "category": req.category,
        "note": "solution corpus is empty until chunk 9 (recommender) lands",
    }
    return PredictResponse(
        output=output,
        explanation=(
            "Recommender stub: no historical resolutions available yet. "
            "Full retrieval against resolved issues lands in chunk 9."
        ),
        confidence=None,
        provider="rule_based",
        model_version=RuleBasedProvider.model_version,
        prompt_version=None,
        latency_ms=0,
        cost_usd=0.0,
        ai_fallback=False,
        fallback_reason=None,
    )
