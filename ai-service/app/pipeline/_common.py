import json
from pathlib import Path

_PROMPTS_DIR = Path(__file__).parent / "prompts"
_prompt_cache: dict[str, str] = {}


def load_prompt(version: str) -> str:
    """version is like 'sentiment/v1' — resolves to prompts/sentiment/v1.txt."""
    cached = _prompt_cache.get(version)
    if cached is not None:
        return cached
    path = _PROMPTS_DIR / f"{version}.txt"
    if not path.exists():
        raise FileNotFoundError(f"Prompt not found: {path}")
    text = path.read_text(encoding="utf-8")
    _prompt_cache[version] = text
    return text


def safe_parse_json(raw: str) -> dict | None:
    """Tolerant JSON parse — strips code fences if a model adds them."""
    s = raw.strip()
    if s.startswith("```"):
        s = s.strip("`")
        if s.startswith("json"):
            s = s[4:]
        s = s.strip()
    try:
        parsed = json.loads(s)
    except json.JSONDecodeError:
        return None
    return parsed if isinstance(parsed, dict) else None
