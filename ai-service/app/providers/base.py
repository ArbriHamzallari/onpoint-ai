from typing import Protocol

from app.schemas import ProviderName, ProviderResult


class Provider(Protocol):
    name: ProviderName

    async def chat(
        self,
        system: str,
        user: str,
        max_tokens: int = 200,
    ) -> ProviderResult: ...
