import hashlib
from uuid import UUID

from app.config import Settings, get_settings
from app.providers.anthropic_provider import AnthropicProvider
from app.providers.base import Provider
from app.providers.openai_provider import OpenAIProvider
from app.providers.rule_based import RuleBasedProvider


def _bucket(business_id: UUID) -> int:
    digest = hashlib.sha256(str(business_id).encode("utf-8")).digest()
    return digest[0] % 100


def pick_provider(business_id: UUID, settings: Settings | None = None) -> Provider:
    s = settings or get_settings()

    if not s.has_openai and not s.has_anthropic:
        return RuleBasedProvider()

    bucket = _bucket(business_id)
    prefer_haiku = bucket < s.haiku_traffic_pct

    if prefer_haiku and s.has_anthropic:
        return AnthropicProvider(s)
    if s.has_openai:
        return OpenAIProvider(s)
    if s.has_anthropic:
        return AnthropicProvider(s)
    return RuleBasedProvider()
