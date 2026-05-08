import uuid

from app.config import override_settings_for_tests, reset_settings
from app.routing import pick_provider


def test_no_keys_returns_rule_based() -> None:
    override_settings_for_tests(openai_api_key=None, anthropic_api_key=None)
    try:
        provider = pick_provider(uuid.uuid4())
        assert provider.name == "rule_based"
    finally:
        reset_settings()


def test_only_openai_key_returns_openai() -> None:
    override_settings_for_tests(
        openai_api_key="sk-test-only-openai",
        anthropic_api_key=None,
        haiku_traffic_pct=35,
    )
    try:
        provider = pick_provider(uuid.uuid4())
        assert provider.name == "openai"
    finally:
        reset_settings()


def test_only_anthropic_key_returns_anthropic() -> None:
    override_settings_for_tests(
        openai_api_key=None,
        anthropic_api_key="ant-test-only",
        haiku_traffic_pct=35,
    )
    try:
        provider = pick_provider(uuid.uuid4())
        assert provider.name == "anthropic"
    finally:
        reset_settings()


def test_provider_choice_is_sticky_per_business_id() -> None:
    override_settings_for_tests(
        openai_api_key="sk-test",
        anthropic_api_key="ant-test",
        haiku_traffic_pct=35,
    )
    try:
        biz = uuid.uuid4()
        first = pick_provider(biz)
        for _ in range(50):
            assert pick_provider(biz).name == first.name
    finally:
        reset_settings()


def test_traffic_split_distribution_within_tolerance() -> None:
    override_settings_for_tests(
        openai_api_key="sk-test",
        anthropic_api_key="ant-test",
        haiku_traffic_pct=35,
    )
    try:
        total = 1000
        haiku = sum(
            1 for _ in range(total)
            if pick_provider(uuid.uuid4()).name == "anthropic"
        )
        pct = haiku / total * 100
        # SHA-256 first byte mod 100 is uniform; ±5pp is generous over 1000.
        assert 30 <= pct <= 40, f"expected ~35% Haiku traffic, got {pct:.1f}%"
    finally:
        reset_settings()


def test_zero_pct_routes_everyone_to_openai() -> None:
    override_settings_for_tests(
        openai_api_key="sk-test",
        anthropic_api_key="ant-test",
        haiku_traffic_pct=0,
    )
    try:
        for _ in range(50):
            assert pick_provider(uuid.uuid4()).name == "openai"
    finally:
        reset_settings()


def test_hundred_pct_routes_everyone_to_anthropic() -> None:
    override_settings_for_tests(
        openai_api_key="sk-test",
        anthropic_api_key="ant-test",
        haiku_traffic_pct=100,
    )
    try:
        for _ in range(50):
            assert pick_provider(uuid.uuid4()).name == "anthropic"
    finally:
        reset_settings()
