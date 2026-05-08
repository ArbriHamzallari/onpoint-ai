import uuid

from fastapi.testclient import TestClient


def _post(client: TestClient, text: str) -> dict:
    resp = client.post(
        "/api/v1/sentiment",
        json={"text": text, "business_id": str(uuid.uuid4())},
    )
    assert resp.status_code == 200, resp.text
    return resp.json()


def test_negative_keyword_maps_to_negative(client: TestClient) -> None:
    body = _post(client, "the AC is broken and freezing in here")
    assert body["output"]["sentiment"] == "negative"
    assert body["output"]["urgency"] >= 0.5
    assert body["provider"] == "rule_based"
    assert body["ai_fallback"] is True
    assert body["fallback_reason"] == "no LLM provider configured"


def test_urgent_keyword_maps_to_high_urgency(client: TestClient) -> None:
    body = _post(client, "there is a fire on the third floor")
    assert body["output"]["sentiment"] == "negative"
    assert body["output"]["urgency"] >= 0.9


def test_positive_keyword_maps_to_positive(client: TestClient) -> None:
    body = _post(client, "the breakfast was amazing, thanks")
    assert body["output"]["sentiment"] == "positive"


def test_neutral_when_no_keywords(client: TestClient) -> None:
    body = _post(client, "I have a question about checkout")
    assert body["output"]["sentiment"] == "neutral"


def test_response_carries_observability_fields(client: TestClient) -> None:
    body = _post(client, "broken AC")
    for key in (
        "output", "explanation", "confidence", "provider",
        "model_version", "latency_ms", "cost_usd", "ai_fallback",
    ):
        assert key in body, f"missing field: {key}"
    assert body["model_version"].startswith("rule_based@")
    assert body["cost_usd"] == 0.0
