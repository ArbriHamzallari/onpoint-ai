import time

from openai import AsyncOpenAI

from app.config import Settings
from app.schemas import ProviderName, ProviderResult

# gpt-4o-mini pricing (USD per 1M tokens). Update when OpenAI revises rates.
_PRICE_IN_PER_1K = 0.00015
_PRICE_OUT_PER_1K = 0.00060


class OpenAIProvider:
    name: ProviderName = "openai"

    def __init__(self, settings: Settings):
        if not settings.has_openai:
            raise RuntimeError("OpenAIProvider constructed without OPENAI_API_KEY")
        self._client = AsyncOpenAI(api_key=settings.openai_api_key)
        self._model = settings.model_gpt_4o_mini

    async def chat(
        self,
        system: str,
        user: str,
        max_tokens: int = 200,
    ) -> ProviderResult:
        start = time.perf_counter()
        resp = await self._client.chat.completions.create(
            model=self._model,
            messages=[
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
            response_format={"type": "json_object"},
            temperature=0.1,
            max_tokens=max_tokens,
        )
        elapsed_ms = int((time.perf_counter() - start) * 1000)
        text = resp.choices[0].message.content or "{}"
        usage = resp.usage
        in_tokens = usage.prompt_tokens if usage else 0
        out_tokens = usage.completion_tokens if usage else 0
        cost = (in_tokens / 1000.0) * _PRICE_IN_PER_1K + (out_tokens / 1000.0) * _PRICE_OUT_PER_1K
        return ProviderResult(
            text=text,
            in_tokens=in_tokens,
            out_tokens=out_tokens,
            cost_usd=round(cost, 6),
            model_version=self._model,
            latency_ms=elapsed_ms,
        )
