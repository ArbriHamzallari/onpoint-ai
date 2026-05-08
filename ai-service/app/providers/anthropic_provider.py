import time

from anthropic import AsyncAnthropic

from app.config import Settings
from app.schemas import ProviderName, ProviderResult

# claude-haiku-4-5 pricing (USD per 1M tokens). Update when Anthropic revises rates.
_PRICE_IN_PER_1K = 0.001
_PRICE_OUT_PER_1K = 0.005


class AnthropicProvider:
    name: ProviderName = "anthropic"

    def __init__(self, settings: Settings):
        if not settings.has_anthropic:
            raise RuntimeError("AnthropicProvider constructed without ANTHROPIC_API_KEY")
        self._client = AsyncAnthropic(api_key=settings.anthropic_api_key)
        self._model = settings.model_haiku

    async def chat(
        self,
        system: str,
        user: str,
        max_tokens: int = 200,
    ) -> ProviderResult:
        start = time.perf_counter()
        resp = await self._client.messages.create(
            model=self._model,
            system=system,
            messages=[{"role": "user", "content": user}],
            max_tokens=max_tokens,
            temperature=0.1,
        )
        elapsed_ms = int((time.perf_counter() - start) * 1000)
        # Anthropic returns content blocks; concatenate text parts.
        parts = [b.text for b in resp.content if getattr(b, "type", "") == "text"]
        text = "".join(parts) or "{}"
        in_tokens = resp.usage.input_tokens
        out_tokens = resp.usage.output_tokens
        cost = (in_tokens / 1000.0) * _PRICE_IN_PER_1K + (out_tokens / 1000.0) * _PRICE_OUT_PER_1K
        return ProviderResult(
            text=text,
            in_tokens=in_tokens,
            out_tokens=out_tokens,
            cost_usd=round(cost, 6),
            model_version=self._model,
            latency_ms=elapsed_ms,
        )
