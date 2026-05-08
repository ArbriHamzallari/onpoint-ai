import time

from app.schemas import ProviderName, ProviderResult


class RuleBasedProvider:
    """
    Deterministic, keyless fallback. Pipeline stages call into per-stage rule
    functions directly rather than going through chat(); chat() exists only to
    satisfy the Provider protocol so routing.pick_provider() can return it
    interchangeably with the LLM providers in tests/dev environments.
    """

    name: ProviderName = "rule_based"
    model_version = "rule_based@1.0.0"

    async def chat(
        self,
        system: str,
        user: str,
        max_tokens: int = 200,
    ) -> ProviderResult:
        start = time.perf_counter()
        elapsed_ms = int((time.perf_counter() - start) * 1000)
        return ProviderResult(
            text="{}",
            in_tokens=0,
            out_tokens=0,
            cost_usd=0.0,
            model_version=self.model_version,
            latency_ms=elapsed_ms,
        )
