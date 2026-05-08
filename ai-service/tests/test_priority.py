import uuid

from fastapi.testclient import TestClient


def _post(client: TestClient, text: str, rating: int | None = None) -> dict:
    payload = {"text": text, "business_id": str(uuid.uuid4())}
    if rating is not None:
        payload["rating"] = rating
    resp = client.post("/api/v1/priority", json=payload)
    assert resp.status_code == 200, resp.text
    return resp.json()


# Pin: the rule-based mapping must mirror FeedbackHandler.cs lines 73-78
# (rating 1 → high, rating 2 → medium, else → low). If FeedbackHandler.cs
# changes, these tests fail and force us to update both sides together.
def test_rating_1_maps_to_high(client: TestClient) -> None:
    body = _post(client, "service was not great", rating=1)
    assert body["output"]["priority_label"] == "high"
    assert 61 <= body["output"]["priority_score"] <= 85


def test_rating_2_maps_to_medium(client: TestClient) -> None:
    body = _post(client, "okay stay", rating=2)
    assert body["output"]["priority_label"] == "medium"
    assert 31 <= body["output"]["priority_score"] <= 60


def test_rating_3_maps_to_low(client: TestClient) -> None:
    body = _post(client, "fine", rating=3)
    assert body["output"]["priority_label"] == "low"


def test_emergency_keyword_overrides_rating(client: TestClient) -> None:
    body = _post(client, "there is a fire in the lobby", rating=5)
    assert body["output"]["priority_label"] == "urgent"
    assert body["output"]["priority_score"] >= 86
