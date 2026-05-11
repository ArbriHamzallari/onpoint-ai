"""
Orchestrator — runs the full 4-stage inference pipeline in one request.

POST /api/v1/pipeline/run

Stages in order:
  1. Sentiment   → {sentiment, urgency}
  2. Classifier  → {category, confidence}   (fed with sentiment context)
  3. Priority    → {priority_score, label}  (fed with sentiment + category)
  4. Router      → {department_key, alts}   (fed with category)

A single call replaces 4 HTTP round-trips. The .NET backend calls this endpoint
and then writes one AiPrediction row per stage in a single DB transaction.
"""

import time
from typing import Any
from uuid import UUID

from fastapi import APIRouter
from pydantic import BaseModel, Field

from app.logging_setup import get_logger
from app.pipeline import classifier as _classifier_mod
from app.pipeline import priority as _priority_mod
from app.pipeline import router as _router_mod
from app.pipeline import sentiment as _sentiment_mod
from app.schemas import PredictRequest, PredictResponse, ProviderName

log = get_logger(__name__)


# ── Schemas ───────────────────────────────────────────────────────────────────


class PipelineRunRequest(BaseModel):
    text: str = Field(min_length=1, max_length=8000)
    business_id: UUID
    session_id: UUID | None = None
    issue_id: UUID | None = None
    feedback_id: UUID | None = None
    rating: int | None = Field(default=None, ge=1, le=5)
    language: str | None = None
    correlation_id: str | None = None


class PipelineStageResult(BaseModel):
    output: dict[str, Any]
    explanation: str
    confidence: float | None = None
    provider: ProviderName
    model_version: str
    prompt_version: str | None = None
    latency_ms: int
    cost_usd: float
    ai_fallback: bool = False
    fallback_reason: str | None = None


class PipelineRunResponse(BaseModel):
    sentiment: PipelineStageResult
    classifier: PipelineStageResult
    priority: PipelineStageResult
    router: PipelineStageResult
    total_latency_ms: int
    total_cost_usd: float


# ── Helpers ───────────────────────────────────────────────────────────────────


def _to_stage(r: PredictResponse) -> PipelineStageResult:
    return PipelineStageResult(
        output=r.output,
        explanation=r.explanation,
        confidence=r.confidence,
        provider=r.provider,
        model_version=r.model_version,
        prompt_version=r.prompt_version,
        latency_ms=r.latency_ms,
        cost_usd=r.cost_usd,
        ai_fallback=r.ai_fallback,
        fallback_reason=r.fallback_reason,
    )


# ── Router ────────────────────────────────────────────────────────────────────

pipeline_router = APIRouter()


@pipeline_router.post("/pipeline/run", response_model=PipelineRunResponse)
async def run_pipeline(req: PipelineRunRequest) -> PipelineRunResponse:
    """Run all four inference stages in sequence and return a combined result.

    Each stage's output is fed as context into subsequent stages:
      - sentiment  → enriches classifier and priority requests
      - classifier → enriches priority and router requests

    The .NET backend calls this single endpoint instead of four individual ones,
    saving ~3 HTTP round-trips per issue submission.
    """
    t0 = time.monotonic()
    cid = req.correlation_id or ""

    base = PredictRequest(
        text=req.text,
        business_id=req.business_id,
        session_id=req.session_id,
        issue_id=req.issue_id,
        feedback_id=req.feedback_id,
        rating=req.rating,
        language=req.language,
        correlation_id=req.correlation_id,
    )

    # ── Stage 1: Sentiment ────────────────────────────────────────────────────
    sentiment_result = await _sentiment_mod.sentiment(base)
    sentiment_label = sentiment_result.output.get("sentiment")
    log.debug("pipeline.sentiment", cid=cid, label=sentiment_label)

    # ── Stage 2: Classifier (enriched with sentiment) ─────────────────────────
    classify_req = base.model_copy(update={"sentiment": sentiment_label})
    classifier_result = await _classifier_mod.classify(classify_req)
    category = classifier_result.output.get("category")
    log.debug("pipeline.classifier", cid=cid, category=category)

    # ── Stage 3: Priority (enriched with sentiment + category) ───────────────
    priority_req = base.model_copy(update={
        "sentiment": sentiment_label,
        "category": category,
    })
    priority_result = await _priority_mod.priority(priority_req)
    log.debug(
        "pipeline.priority",
        cid=cid,
        score=priority_result.output.get("priority_score"),
    )

    # ── Stage 4: Router (enriched with category) ──────────────────────────────
    route_req = base.model_copy(update={"category": category})
    router_result = await _router_mod.route(route_req)
    log.debug(
        "pipeline.router",
        cid=cid,
        dept=router_result.output.get("department_key"),
    )

    elapsed_ms = int((time.monotonic() - t0) * 1000)
    total_cost = (
        sentiment_result.cost_usd
        + classifier_result.cost_usd
        + priority_result.cost_usd
        + router_result.cost_usd
    )

    log.info(
        "pipeline.complete",
        cid=cid,
        issue_id=str(req.issue_id) if req.issue_id else None,
        total_ms=elapsed_ms,
        total_cost_usd=total_cost,
        category=category,
        dept=router_result.output.get("department_key"),
        any_fallback=any([
            sentiment_result.ai_fallback,
            classifier_result.ai_fallback,
            priority_result.ai_fallback,
            router_result.ai_fallback,
        ]),
    )

    return PipelineRunResponse(
        sentiment=_to_stage(sentiment_result),
        classifier=_to_stage(classifier_result),
        priority=_to_stage(priority_result),
        router=_to_stage(router_result),
        total_latency_ms=elapsed_ms,
        total_cost_usd=total_cost,
    )
