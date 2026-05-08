import pytest
from fastapi.testclient import TestClient

from app.config import override_settings_for_tests, reset_settings
from app.main import app


@pytest.fixture(autouse=True)
def _isolate_settings():
    # Default test environment has no LLM keys, so every stage takes the
    # rule_based path. Tests that need keys override explicitly.
    override_settings_for_tests(openai_api_key=None, anthropic_api_key=None)
    yield
    reset_settings()


@pytest.fixture
def client() -> TestClient:
    return TestClient(app)
