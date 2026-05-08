from fastapi import APIRouter

from app.pipeline._common import load_prompt, safe_parse_json
from app.providers.rule_based import RuleBasedProvider
from app.routing import pick_provider
from app.schemas import PredictRequest, PredictResponse

PROMPT_VERSION = "classifier/v1"

_VALID_CATEGORIES = {
    "hvac", "plumbing", "housekeeping", "noise", "front_desk",
    "food_beverage", "maintenance", "security", "other",
}

_KEYWORD_MAP: tuple[tuple[str, tuple[str, ...]], ...] = (
    ("hvac",          ("ac", "air condition", "heater", "heating", "hot", "cold", "freezing", "thermostat")),
    ("plumbing",      ("water", "leak", "toilet", "shower", "drain", "sink", "faucet", "pipe")),
    ("housekeeping",  ("clean", "dirty", "towel", "linen", "bed", "trash", "stained", "smell")),
    ("noise",         ("noise", "loud", "music", "neighbor", "quiet", "construction")),
    ("front_desk",    ("check in", "check-in", "check out", "check-out", "billing", "reservation", "key card", "keycard")),
    ("food_beverage", ("food", "restaurant", "breakfast", "menu", "minibar", "room service", "dinner", "lunch")),
    ("security",      ("theft", "stolen", "intrud", "lock", "safe", "suspicious")),
    ("maintenance",   ("broken", "not working", "doesn't work", "light", "tv", "outlet", "elevator", "wifi", "internet")),
)


def _rule_based(text: str) -> tuple[dict, float]:
    lower = text.lower()
    for category, keywords in _KEYWORD_MAP:
        if any(k in lower for k in keywords):
            return ({"category": category}, 0.65)
    return ({"category": "other"}, 0.4)


def _fallback(text: str, reason: str) -> PredictResponse:
    output, conf = _rule_based(text)
    return PredictResponse(
        output=output,
        explanation=f"Rule-based fallback: keyword match → {output['category']}.",
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


@router.post("/classify", response_model=PredictResponse)
async def classify(req: PredictRequest) -> PredictResponse:
    provider = pick_provider(req.business_id)

    if isinstance(provider, RuleBasedProvider):
        return _fallback(req.text, "no LLM provider configured")

    system = load_prompt(PROMPT_VERSION)
    try:
        result = await provider.chat(system=system, user=req.text, max_tokens=150)
        parsed = safe_parse_json(result.text)
        if not parsed or "category" not in parsed:
            raise ValueError(f"invalid LLM JSON: {result.text[:200]}")
        category = str(parsed["category"]).lower()
        if category not in _VALID_CATEGORIES:
            category = "other"
        return PredictResponse(
            output={"category": category},
            explanation=str(parsed.get("explanation", "")),
            confidence=float(parsed.get("confidence", 0.8)),
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
