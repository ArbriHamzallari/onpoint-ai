from typing import Any, Literal
from uuid import UUID

from pydantic import BaseModel, Field

ProviderName = Literal["openai", "anthropic", "rule_based", "custom"]


class PredictRequest(BaseModel):
    text: str = Field(min_length=1, max_length=8000)
    business_id: UUID
    session_id: UUID | None = None
    issue_id: UUID | None = None
    feedback_id: UUID | None = None
    language: str | None = None
    correlation_id: str | None = None

    rating: int | None = Field(default=None, ge=1, le=5)
    sentiment: str | None = None
    category: str | None = None
    department_key: str | None = None


class PredictResponse(BaseModel):
    output: dict[str, Any]
    explanation: str
    confidence: float | None = Field(default=None, ge=0.0, le=1.0)
    provider: ProviderName
    model_version: str
    prompt_version: str | None = None
    latency_ms: int = Field(ge=0)
    cost_usd: float = Field(ge=0.0)
    ai_fallback: bool = False
    fallback_reason: str | None = None


class ProviderResult(BaseModel):
    text: str
    in_tokens: int
    out_tokens: int
    cost_usd: float
    model_version: str
    latency_ms: int
