from fastapi import APIRouter

from app.pipeline._common import load_prompt, safe_parse_json
from app.providers.rule_based import RuleBasedProvider
from app.routing import pick_provider
from app.schemas import PredictRequest, PredictResponse

PROMPT_VERSION = "priority/v1"

_URGENT_KEYWORDS = (
    "fire", "flood", "smoke", "ambulance", "injury", "bleeding", "gas",
    "electrocut", "child trapped", "emergency",
)
_HIGH_KEYWORDS = (
    "broken", "leak", "no water", "no hot water", "no power", "freezing",
    "burning", "stolen", "intrud",
)


def _label_for_score(score: int) -> str:
    if score >= 86:
        return "urgent"
    if score >= 61:
        return "high"
    if score >= 31:
        return "medium"
    return "low"


def _rule_based(text: str, rating: int | None) -> tuple[dict, float]:
    lower = text.lower()

    if any(k in lower for k in _URGENT_KEYWORDS):
        score = 95
    elif rating == 1:
        score = 80
    elif any(k in lower for k in _HIGH_KEYWORDS):
        score = 70
    elif rating == 2:
        score = 50
    elif rating == 3:
        score = 25
    else:
        score = 20

    return (
        {"priority_score": score, "priority_label": _label_for_score(score)},
        0.6,
    )


def _fallback(req: PredictRequest, reason: str) -> PredictResponse:
    output, conf = _rule_based(req.text, req.rating)
    return PredictResponse(
        output=output,
        explanation=(
            f"Rule-based fallback: rating={req.rating}, "
            f"score={output['priority_score']} → {output['priority_label']}."
        ),
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


@router.post("/priority", response_model=PredictResponse)
async def priority(req: PredictRequest) -> PredictResponse:
    provider = pick_provider(req.business_id)

    if isinstance(provider, RuleBasedProvider):
        return _fallback(req, "no LLM provider configured")

    system = load_prompt(PROMPT_VERSION)
    user = req.text
    if req.rating is not None:
        user = f"{user}\n\n[context] rating={req.rating}/5"
    if req.sentiment:
        user = f"{user}\n[context] sentiment={req.sentiment}"

    try:
        result = await provider.chat(system=system, user=user, max_tokens=200)
        parsed = safe_parse_json(result.text)
        if not parsed or "priority_score" not in parsed:
            raise ValueError(f"invalid LLM JSON: {result.text[:200]}")
        score = int(parsed["priority_score"])
        score = max(0, min(100, score))
        label = parsed.get("priority_label") or _label_for_score(score)
        return PredictResponse(
            output={"priority_score": score, "priority_label": str(label)},
            explanation=str(parsed.get("explanation", "")),
            confidence=0.85,
            provider=provider.name,
            model_version=result.model_version,
            prompt_version=PROMPT_VERSION,
            latency_ms=result.latency_ms,
            cost_usd=result.cost_usd,
            ai_fallback=False,
            fallback_reason=None,
        )
    except Exception as exc:
        return _fallback(req, f"llm_error: {exc}")
