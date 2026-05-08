import uuid

from fastapi.testclient import TestClient


def _post(client: TestClient, text: str, category: str | None = None) -> dict:
    payload = {"text": text, "business_id": str(uuid.uuid4())}
    if category:
        payload["category"] = category
    resp = client.post("/api/v1/route", json=payload)
    assert resp.status_code == 200, resp.text
    return resp.json()


def test_ac_text_routes_to_maintenance(client: TestClient) -> None:
    body = _post(client, "AC is broken")
    assert body["output"]["department_key"] == "maintenance"
    assert body["provider"] == "rule_based"


def test_dirty_text_routes_to_housekeeping(client: TestClient) -> None:
    body = _post(client, "towels are dirty")
    assert body["output"]["department_key"] == "housekeeping"


def test_category_overrides_keyword_when_provided(client: TestClient) -> None:
    body = _post(client, "general issue", category="plumbing")
    assert body["output"]["department_key"] == "maintenance"


def test_includes_alternatives(client: TestClient) -> None:
    body = _post(client, "the AC is broken")
    assert isinstance(body["output"]["alternatives"], list)
