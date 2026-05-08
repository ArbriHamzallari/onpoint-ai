import uuid

from fastapi.testclient import TestClient


def _post(client: TestClient, text: str) -> dict:
    resp = client.post(
        "/api/v1/classify",
        json={"text": text, "business_id": str(uuid.uuid4())},
    )
    assert resp.status_code == 200, resp.text
    return resp.json()


def test_ac_text_classified_as_hvac(client: TestClient) -> None:
    body = _post(client, "the AC is not working in my room")
    assert body["output"]["category"] == "hvac"
    assert body["provider"] == "rule_based"


def test_leak_text_classified_as_plumbing(client: TestClient) -> None:
    body = _post(client, "there is a water leak in the bathroom")
    assert body["output"]["category"] == "plumbing"


def test_dirty_text_classified_as_housekeeping(client: TestClient) -> None:
    body = _post(client, "the towels are dirty and the room smells")
    assert body["output"]["category"] == "housekeeping"


def test_unknown_text_classified_as_other(client: TestClient) -> None:
    body = _post(client, "zzz qqq")
    assert body["output"]["category"] == "other"
