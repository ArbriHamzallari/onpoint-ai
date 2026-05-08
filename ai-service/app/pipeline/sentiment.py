from fastapi import APIRouter

from app.pipeline._common import load_prompt, safe_parse_json
from app.providers.rule_based import RuleBasedProvider
from app.routing import pick_provider
from app.schemas import PredictRequest, PredictResponse

PROMPT_VERSION = "sentiment/v1"

_URGENT_KEYWORDS = (
    "fire", "flood", "smoke", "ambulance", "injury", "bleeding", "gas",
    "electrocut", "child trapped", "emergency", "broken glass",
)
_NEGATIVE_KEYWORDS = (
    "broken", "leak", "dirty", "smell", "rude", "terrible", "awful", "cold",
    "freezing", "hot", "noisy", "loud", "slow", "missing", "stained", "moldy",
    "horrible", "worst", "disgusting", "scam",
)
_POSITIVE_KEYWORDS = (
    "great", "amazing", "love", "excellent", "perfect", "wonderful",
    "fantastic", "awesome", "thank", "thanks", "happy", "delicious",
)


def _rule_based(text: str) -> tuple[dict, float]:
    lower = text.lower()
    if any(k in lower for k in _URGENT_KEYWORDS):
        return ({"sentiment": "negative", "urgency": 0.95}, 0.75)
    if any(k in lower for k in _NEGATIVE_KEYWORDS):
        return ({"sentiment": "negative", "urgency": 0.65}, 0.65)
    if any(k in lower for k in _POSITIVE_KEYWORDS):
        return ({"sentiment": "positive", "urgency": 0.1}, 0.65)
    return ({"sentiment": "neutral", "urgency": 0.3}, 0.5)


def _fallback(text: str, reason: str) -> PredictResponse:
    output, conf = _rule_based(text)
    return PredictResponse(
        output=output,
        explanation=f"Rule-based fallback: {output['sentiment']} (urgency {output['urgency']}).",
        confidence=conf,
        provider="rule_based",
        model_version=RuleBasedProvider.model_version,
        prompt_version=None,
        latency_ms=0,
        cost_usd=0.0,
        ai_fallback=True,
        fallback_reason=reason,
    )


router = APIRouter()


@router.post("/sentiment", response_model=PredictResponse)
async def sentiment(req: PredictRequest) -> PredictResponse:
    provider = pick_provider(req.business_id)

    if isinstance(provider, RuleBasedProvider):
        return _fallback(req.text, "no LLM provider configured")

    system = load_prompt(PROMPT_VERSION)
    try:
        result = await provider.chat(system=system, user=req.text, max_tokens=200)
        parsed = safe_parse_json(result.text)
        if not parsed or "sentiment" not in parsed:
            raise ValueError(f"invalid LLM JSON: {result.text[:200]}")
        return PredictResponse(
            output={
                "sentiment": str(parsed["sentiment"]),
                "urgency": float(parsed.get("urgency", 0.3)),
            },
            explanation=str(parsed.get("explanation", "")),
            confidence=0.88,
            provider=provider.name,
            model_version=result.model_version,
            prompt_version=PROMPT_VERSION,
            latency_ms=result.latency_ms,
            cost_usd=result.cost_usd,
            ai_fallback=False,
            fallback_reason=None,
        )
    except Exception as exc:
        return _fallback(req.text, f"llm_error: {exc}")
