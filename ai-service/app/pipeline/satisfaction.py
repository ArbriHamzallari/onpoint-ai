from fastapi import APIRouter

from app.providers.rule_based import RuleBasedProvider
from app.schemas import PredictRequest, PredictResponse

router = APIRouter()


_SENTIMENT_TO_BASE = {
    "positive": 4.5,
    "neutral":  3.5,
    "negative": 2.0,
    "unknown":  3.0,
}


@router.post("/satisfaction", response_model=PredictResponse)
async def satisfaction(req: PredictRequest) -> PredictResponse:
    """
    Predicted-satisfaction stage. Full regression model lands in chunk 9
    (CLAUDE.md AI pipeline stage 8). For now: a coarse baseline derived from
    the rating + sentiment, just to give the dashboard something non-null
    to render. Confidence stays low so .NET treats it as advisory only.
    """
    base = _SENTIMENT_TO_BASE.get(req.sentiment or "unknown", 3.0)
    if req.rating is not None:
        base = (base + float(req.rating)) / 2.0
    predicted = round(max(1.0, min(5.0, base)), 2)

    output = {
        "predicted_satisfaction": predicted,
        "scale": "1-5",
    }
    return PredictResponse(
        output=output,
        explanation=(
            f"Stub baseline: blended rating={req.rating} and "
            f"sentiment={req.sentiment} → {predicted}. Full regressor lands in chunk 9."
        ),
        confidence=0.4,
        provider="rule_based",
        model_version=RuleBasedProvider.model_version,
        prompt_version=None,
        latency_ms=0,
        cost_usd=0.0,
        ai_fallback=False,
        fallback_reason=None,
    )
