from fastapi import APIRouter

from app.pipeline._common import load_prompt, safe_parse_json
from app.providers.rule_based import RuleBasedProvider
from app.routing import pick_provider
from app.schemas import PredictRequest, PredictResponse

PROMPT_VERSION = "router/v1"

_VALID_DEPARTMENTS = {
    "maintenance", "housekeeping", "food_beverage",
    "front_desk", "security", "management", "other",
}

_CATEGORY_TO_DEPARTMENT = {
    "hvac": "maintenance",
    "plumbing": "maintenance",
    "maintenance": "maintenance",
    "noise": "front_desk",
    "housekeeping": "housekeeping",
    "food_beverage": "food_beverage",
    "front_desk": "front_desk",
    "security": "security",
    "other": "other",
}

_KEYWORD_MAP: tuple[tuple[str, tuple[str, ...]], ...] = (
    ("maintenance",   ("ac", "broken", "leak", "water", "light", "tv", "elevator", "wifi", "thermostat")),
    ("housekeeping",  ("clean", "dirty", "towel", "linen", "bed", "trash", "smell")),
    ("food_beverage", ("food", "restaurant", "breakfast", "minibar", "room service")),
    ("front_desk",    ("check in", "check-in", "check out", "check-out", "billing", "reservation", "noise")),
    ("security",      ("theft", "stolen", "intrud", "lock", "safe")),
    ("management",    ("manager", "complaint about staff", "rude staff")),
)


def _rule_based(text: str, category: str | None) -> tuple[dict, float]:
    if category and category in _CATEGORY_TO_DEPARTMENT:
        primary = _CATEGORY_TO_DEPARTMENT[category]
        return (
            {"department_key": primary, "alternatives": ["management"]},
            0.7,
        )

    lower = text.lower()
    for dept, keywords in _KEYWORD_MAP:
        if any(k in lower for k in keywords):
            alts = ["management"] if dept != "management" else ["front_desk"]
            return ({"department_key": dept, "alternatives": alts}, 0.6)

    return ({"department_key": "other", "alternatives": ["management"]}, 0.4)


def _fallback(req: PredictRequest, reason: str) -> PredictResponse:
    output, conf = _rule_based(req.text, req.category)
    return PredictResponse(
        output=output,
        explanation=f"Rule-based fallback: routed to {output['department_key']}.",
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


@router.post("/route", response_model=PredictResponse)
async def route(req: PredictRequest) -> PredictResponse:
    provider = pick_provider(req.business_id)

    if isinstance(provider, RuleBasedProvider):
        return _fallback(req, "no LLM provider configured")

    system = load_prompt(PROMPT_VERSION)
    user = req.text
    if req.category:
        user = f"{user}\n\n[context] category={req.category}"

    try:
        result = await provider.chat(system=system, user=user, max_tokens=200)
        parsed = safe_parse_json(result.text)
        if not parsed or "department_key" not in parsed:
            raise ValueError(f"invalid LLM JSON: {result.text[:200]}")
        dept = str(parsed["department_key"]).lower()
        if dept not in _VALID_DEPARTMENTS:
            dept = "other"
        alts_raw = parsed.get("alternatives") or []
        alts = [str(a).lower() for a in alts_raw if str(a).lower() in _VALID_DEPARTMENTS]
        return PredictResponse(
            output={"department_key": dept, "alternatives": alts},
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
        return _fallback(req, f"llm_error: {exc}")
