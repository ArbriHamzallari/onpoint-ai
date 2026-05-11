namespace OnPoint.Application.Ai;

public interface IAiService
{
    /// <summary>
    /// Runs the 4-stage inference pipeline (sentiment → classify → priority → route).
    /// Returns <see langword="null"/> when the AI service is unreachable.
    /// Callers MUST tag the issue with AiFallback=true on null return and log a
    /// warning (CLAUDE.md §AI Engineering #5 — graceful degradation, never silent).
    /// Never throws on network or timeout failures; only throws on programming errors.
    /// </summary>
    Task<AiPipelineResult?> RunPipelineAsync(
        AiPipelineRequest request,
        CancellationToken ct = default);
}
