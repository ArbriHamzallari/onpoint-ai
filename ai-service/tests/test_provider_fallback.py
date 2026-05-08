import uuid

import pytest
from fastapi.testclient import TestClient

from app.schemas import ProviderName, ProviderResult


class _RaisingProvider:
    name: ProviderName = "openai"

    async def chat(self, system: str, user: str, max_tokens: int = 200) -> ProviderResult:
        raise RuntimeError("simulated upstream outage")


@pytest.fixture
def raising_provider(monkeypatch):
    fake = _RaisingProvider()

    # Override pick_provider in every stage that imports it. Patching the
    # module attribute (not the source) is the FastAPI way — each route
    # closes over the imported name.
    for module in (
        "app.pipeline.sentiment",
        "app.pipeline.classifier",
        "app.pipeline.priority",
        "app.pipeline.router",
    ):
        monkeypatch.setattr(f"{module}.pick_provider", lambda biz_id: fake)
    return fake


def test_sentiment_falls_back_when_provider_raises(
    client: TestClient,
    raising_provider,
) -> None:
    resp = client.post(
        "/api/v1/sentiment",
        json={"text": "broken AC", "business_id": str(uuid.uuid4())},
    )
    assert resp.status_code == 200
    body = resp.json()
    assert body["ai_fallback"] is True
    assert body["provider"] == "rule_based"
    assert "simulated upstream outage" in body["fallback_reason"]
    assert body["output"]["sentiment"] == "negative"


def test_classifier_falls_back_when_provider_raises(
    client: TestClient,
    raising_provider,
) -> None:
    resp = client.post(
        "/api/v1/classify",
        json={"text": "AC is broken", "business_id": str(uuid.uuid4())},
    )
    body = resp.json()
    assert body["ai_fallback"] is True
    assert body["output"]["category"] == "hvac"


def test_priority_falls_back_when_provider_raises(
    client: TestClient,
    raising_provider,
) -> None:
    resp = client.post(
        "/api/v1/priority",
        json={"text": "service was poor", "business_id": str(uuid.uuid4()), "rating": 1},
    )
    body = resp.json()
    assert body["ai_fallback"] is True
    assert body["output"]["priority_label"] == "high"


def test_router_falls_back_when_provider_raises(
    client: TestClient,
    raising_provider,
) -> None:
    resp = client.post(
        "/api/v1/route",
        json={"text": "AC is broken", "business_id": str(uuid.uuid4())},
    )
    body = resp.json()
    assert body["ai_fallback"] is True
    assert body["output"]["department_key"] == "maintenance"
