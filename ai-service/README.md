# OnPoint AI Service

Python FastAPI inference service for OnPoint. Exposes one HTTP route per
pipeline stage. The .NET backend (chunk 4c) calls these routes and stores
every prediction in `ai_predictions`.

## Pipeline stages

| Route | Stage | Phase |
|---|---|---|
| `POST /api/v1/sentiment` | sentiment + urgency | 4 |
| `POST /api/v1/classify` | category | 4 |
| `POST /api/v1/priority` | priority score 0-100 | 4 |
| `POST /api/v1/route` | department routing | 4 |
| `POST /api/v1/match` | staff matching (rule-based for now) | 4 |
| `POST /api/v1/recommend` | top-5 historical solutions (stub) | 8 |
| `POST /api/v1/satisfaction` | predicted satisfaction (stub) | 8 |
| `POST /api/v1/chat` | guest chatbot reply (stub) | 8 |

## A/B routing

- **Default:** `gpt-4o-mini` (~65% traffic)
- **Variant:** `claude-haiku-4-5-20251001` (~35% traffic)
- **Sticky per `business_id`** — the same tenant always hits the same provider
  during the experiment so latency/satisfaction comparisons stay clean.
- **Hash function:** SHA-256 of `business_id` mod 100; bucket < `HAIKU_TRAFFIC_PCT`
  goes to Haiku.
- **No keys → rule-based fallback** for everything. Tests run keyless.

## Local run (Windows / PowerShell)

```powershell
cd ai-service
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e ".[dev]"
copy .env.example .env

pytest -v
ruff check .

uvicorn app.main:app --reload --port 8000
```

## Sanity checks

```powershell
curl http://localhost:8000/health

curl -X POST http://localhost:8000/api/v1/sentiment `
  -H "Content-Type: application/json" `
  -d '{\"text\":\"The AC is broken and it is freezing in here\",\"business_id\":\"00000000-0000-0000-0000-000000000001\"}'
```

## Docker

```powershell
docker build -t onpoint-ai-service .
docker run -p 5200:8000 --env-file .env onpoint-ai-service
curl http://localhost:5200/health
```

Or via the root `docker-compose.yml`:

```powershell
docker compose up ai-service
```

## Layout

```
ai-service/
├── app/
│   ├── main.py              FastAPI app + /health
│   ├── config.py            env-driven settings
│   ├── logging_setup.py     structlog JSON logs
│   ├── schemas.py           PredictRequest / PredictResponse
│   ├── routing.py           A/B provider chooser
│   ├── providers/           openai, anthropic, rule_based
│   ├── pipeline/            one router module per stage
│   │   └── prompts/         versioned prompt templates (per CLAUDE.md AI rule 9)
│   ├── models/              future model weights mount point
│   └── learning/            future retraining hooks
└── tests/                   pytest, runs keyless via rule_based fallback
```

## Constraints (from CLAUDE.md)

- **Inference latency budget < 100ms per stage.** Each response includes
  `latency_ms` so .NET can enforce.
- **Every response includes** `provider`, `model_version`, `prompt_version`,
  `confidence`, `cost_usd`, `ai_fallback`, `fallback_reason`, `explanation` —
  enough for the .NET side to write a complete `ai_predictions` row.
- **Graceful degradation.** If the chosen LLM provider raises, we fall through
  to `rule_based` and tag `ai_fallback=true` with the exception message.
- **No PII handling here.** The DB-side `contains_pii=TRUE` flag on
  `ai_predictions` controls training-corpus extraction; raw input flows freely
  through inference.
- **Cost ceiling < $0.01 per issue.** Even worst-case 4 Haiku calls ≈ $0.0024.
