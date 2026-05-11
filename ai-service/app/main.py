import uuid

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.config import get_settings
from app.logging_setup import configure_logging, get_logger
from app.pipeline import (
    chatbot,
    classifier,
    matcher,
    orchestrator,
    priority,
    recommender,
    router,
    satisfaction,
    sentiment,
)

settings = get_settings()
configure_logging(settings.log_level)
log = get_logger(__name__)

app = FastAPI(
    title="OnPoint AI Service",
    version="0.1.0",
    description="Inference service for the OnPoint hospitality AI pipeline.",
)


@app.middleware("http")
async def correlation_and_logging(request: Request, call_next):
    correlation_id = request.headers.get("x-correlation-id") or str(uuid.uuid4())
    request.state.correlation_id = correlation_id
    log.info("request.start", method=request.method, path=request.url.path, cid=correlation_id)
    try:
        response = await call_next(request)
    except Exception as exc:
        log.exception("request.unhandled", cid=correlation_id, error=str(exc))
        return JSONResponse(
            status_code=500,
            content={"error": "internal_error", "correlation_id": correlation_id},
        )
    response.headers["x-correlation-id"] = correlation_id
    log.info(
        "request.end",
        method=request.method,
        path=request.url.path,
        cid=correlation_id,
        status=response.status_code,
    )
    return response


@app.get("/health")
async def health() -> dict[str, str]:
    return {
        "status": "ok",
        "version": app.version,
        "environment": settings.environment,
    }


app.include_router(sentiment.router, prefix="/api/v1", tags=["pipeline"])
app.include_router(classifier.router, prefix="/api/v1", tags=["pipeline"])
app.include_router(priority.router, prefix="/api/v1", tags=["pipeline"])
app.include_router(router.router, prefix="/api/v1", tags=["pipeline"])
app.include_router(matcher.router, prefix="/api/v1", tags=["pipeline"])
app.include_router(recommender.router, prefix="/api/v1", tags=["pipeline"])
app.include_router(satisfaction.router, prefix="/api/v1", tags=["pipeline"])
app.include_router(chatbot.router, prefix="/api/v1", tags=["pipeline"])
# Orchestrator: single endpoint that runs all 4 pipeline stages in one call
app.include_router(orchestrator.pipeline_router, prefix="/api/v1", tags=["pipeline"])
