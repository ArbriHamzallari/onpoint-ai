from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
        case_sensitive=False,
    )

    openai_api_key: str | None = None
    anthropic_api_key: str | None = None

    model_gpt_4o_mini: str = "gpt-4o-mini"
    model_haiku: str = "claude-haiku-4-5-20251001"

    haiku_traffic_pct: int = Field(default=35, ge=0, le=100)

    log_level: str = "INFO"
    environment: str = "development"

    @property
    def has_openai(self) -> bool:
        return bool(self.openai_api_key and self.openai_api_key.strip())

    @property
    def has_anthropic(self) -> bool:
        return bool(self.anthropic_api_key and self.anthropic_api_key.strip())


_settings: Settings | None = None


def get_settings() -> Settings:
    global _settings
    if _settings is None:
        _settings = Settings()
    return _settings


def override_settings_for_tests(**kwargs: object) -> Settings:
    global _settings
    _settings = Settings(**kwargs)  # type: ignore[arg-type]
    return _settings


def reset_settings() -> None:
    global _settings
    _settings = None
