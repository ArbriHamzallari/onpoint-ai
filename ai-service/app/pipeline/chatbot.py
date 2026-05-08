from fastapi import APIRouter

from app.providers.rule_based import RuleBasedProvider
from app.schemas import PredictRequest, PredictResponse

router = APIRouter()


@router.post("/chat", response_model=PredictResponse)
async def chat(req: PredictRequest) -> PredictResponse:
    """
    Guest chatbot. Full hosted-LLM implementation with hotel-knowledge-base
    retrieval lands in chunk 9 (CLAUDE.md AI pipeline stage 9). The current
    response is a fixed acknowledgement so the guest UI can wire the route
    and exercise the full request/response shape today.
    """
    output = {
        "reply": (
            "Thanks — your issue was received and a team member is on it. "
            "Live chat with our AI assistant will be available shortly."
        ),
        "language": req.language or "en",
    }
    return PredictResponse(
        output=output,
        explanation="Chatbot stub: returns a static acknowledgement until chunk 9.",
        confidence=None,
        provider="rule_based",
        model_version=RuleBasedProvider.model_version,
        prompt_version=None,
        latency_ms=0,
        cost_usd=0.0,
        ai_fallback=False,
        fallback_reason=None,
    )
