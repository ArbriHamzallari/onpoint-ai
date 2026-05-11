"""
Tests for POST /api/v1/pipeline/run (orchestrator endpoint).

All tests run with no LLM keys configured (conftest autouse fixture), so every
stage takes the rule_based path — deterministic and fast.
"""

import uuid

from fastapi.testclient import TestClient


def _run(client: TestClient, text: str, rating: int | None = None) -> dict:
    payload: dict = {
        "text": text,
        "business_id": str(uuid.uuid4()),
    }
    if rating is not None:
        payload["rating"] = rating
    resp = client.post("/api/v1/pipeline/run", json=payload)
    assert resp.status_code == 200, resp.text
    return resp.json()


# ── Shape tests ───────────────────────────────────────────────────────────────


def test_all_four_stages_present(client: TestClient) -> None:
    body = _run(client, "the air conditioner is not working and the room is hot")
    for stage in ("sentiment", "classifier", "priority", "router"):
        assert stage in body, f"missing stage: {stage}"
        assert "output" in body[stage]
        assert "provider" in body[stage]
        assert "latency_ms" in body[stage]
        assert "ai_fallback" in body[stage]
    assert "total_latency_ms" in body
    assert "total_cost_usd" in body


def test_response_shape_has_all_observability_fields(client: TestClient) -> None:
    body = _run(client, "wifi is very slow")
    for stage in ("sentiment", "classifier", "priority", "router"):
        stage_data = body[stage]
        for field in (
            "output", "explanation", "confidence", "provider",
            "model_version", "latency_ms", "cost_usd", "ai_fallback",
        ):
            assert field in stage_data, f"stage '{stage}' missing field '{field}'"


# ── Fallback / rule-based behaviour ───────────────────────────────────────────


def test_all_stages_use_rule_based_when_no_llm_keys(client: TestClient) -> None:
    body = _run(client, "toilet is leaking badly")
    for stage in ("sentiment", "classifier", "priority", "router"):
        assert body[stage]["provider"] == "rule_based", \
            f"stage '{stage}' did not use rule_based provider"
        assert body[stage]["ai_fallback"] is True, \
            f"stage '{stage}' did not set ai_fallback=true"


def test_fallback_reason_is_set(client: TestClient) -> None:
    body = _run(client, "broken AC")
    for stage in ("sentiment", "classifier", "priority", "router"):
        assert body[stage]["fallback_reason"] is not None, \
            f"stage '{stage}' missing fallback_reason"


# ── Cross-stage context propagation ──────────────────────────────────────────


def test_negative_text_yields_negative_sentiment(client: TestClient) -> None:
    body = _run(client, "this is terrible and disgusting")
    sentiment_label = body["sentiment"]["output"]["sentiment"]
    assert sentiment_label in ("negative", "urgent"), \
        f"unexpected sentiment: {sentiment_label}"


def test_maintenance_text_routes_to_maintenance_department(client: TestClient) -> None:
    body = _run(client, "the shower drain is completely clogged")
    dept = body["router"]["output"]["department_key"]
    # Rule-based: "drain" → plumbing → maintenance or other (acceptable)
    assert dept in ("maintenance", "other", "plumbing"), \
        f"unexpected department: {dept}"


def test_hvac_keyword_classifies_as_hvac_or_maintenance(client: TestClient) -> None:
    body = _run(client, "the air conditioning keeps turning off")
    category = body["classifier"]["output"]["category"]
    assert category in ("hvac", "maintenance", "other"), \
        f"unexpected category: {category}"


# ── Priority integration ───────────────────────────────────────────────────────


def test_urgent_keyword_gets_high_priority_score(client: TestClient) -> None:
    body = _run(client, "there is a fire on the third floor", rating=1)
    score = body["priority"]["output"]["priority_score"]
    assert score >= 80, f"expected high score for fire emergency, got {score}"


def test_low_urgency_text_gets_lower_priority_than_emergency(
    client: TestClient,
) -> None:
    body_normal = _run(client, "the light bulb in the bathroom is out", rating=3)
    body_fire = _run(client, "there is a fire on the third floor", rating=1)
    assert body_fire["priority"]["output"]["priority_score"] > \
           body_normal["priority"]["output"]["priority_score"]


# ── Aggregation ───────────────────────────────────────────────────────────────


def test_total_latency_and_cost_are_non_negative(client: TestClient) -> None:
    body = _run(client, "light bulb in the bathroom is out", rating=2)
    assert body["total_latency_ms"] >= 0
    assert body["total_cost_usd"] >= 0.0


def test_total_cost_equals_sum_of_stage_costs(client: TestClient) -> None:
    body = _run(client, "minibar was not restocked")
    expected = sum(
        body[stage]["cost_usd"]
        for stage in ("sentiment", "classifier", "priority", "router")
    )
    assert abs(body["total_cost_usd"] - expected) < 1e-9, \
        f"total_cost_usd {body['total_cost_usd']} != sum of stages {expected}"


# ── Optional fields ───────────────────────────────────────────────────────────


def test_optional_ids_are_accepted(client: TestClient) -> None:
    biz = str(uuid.uuid4())
    resp = client.post("/api/v1/pipeline/run", json={
        "text": "the key card does not work",
        "business_id": biz,
        "session_id": str(uuid.uuid4()),
        "issue_id": str(uuid.uuid4()),
        "feedback_id": str(uuid.uuid4()),
        "rating": 2,
        "correlation_id": "test-corr-1",
    })
    assert resp.status_code == 200, resp.text


def test_empty_text_is_rejected(client: TestClient) -> None:
    resp = client.post("/api/v1/pipeline/run", json={
        "text": "",
        "business_id": str(uuid.uuid4()),
    })
    assert resp.status_code == 422, "empty text should be rejected by validation"
