namespace OnPoint.Application.Ai;

/// <summary>
/// In-memory channel that decouples the guest-facing request from AI inference.
/// FeedbackHandler enqueues one item per created issue; AiPipelineBackgroundService
/// drains. Fire-and-forget — the guest response returns before AI inference completes.
/// </summary>
public interface IAiPipelineQueue
{
    void Enqueue(AiPipelineRequest request);
}
