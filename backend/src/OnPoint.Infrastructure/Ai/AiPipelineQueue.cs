using System.Threading.Channels;
using OnPoint.Application.Ai;

namespace OnPoint.Infrastructure.Ai;

/// <summary>
/// Singleton in-memory bounded channel. FeedbackHandler writes one item per created
/// issue; AiPipelineBackgroundService drains one item at a time.
///
/// Capacity 1 000. When full, DropOldest discards the oldest pending item rather than
/// blocking the guest response. Dropped items are not retried in this phase — the issue
/// simply displays without AI fields until the next matching submission or a manual
/// override. This is acceptable: the background service is single-reader and should
/// stay near-empty under normal load.
/// </summary>
public sealed class AiPipelineQueue : IAiPipelineQueue
{
    private const int Capacity = 1_000;

    private readonly Channel<AiPipelineRequest> _channel =
        Channel.CreateBounded<AiPipelineRequest>(new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            FullMode     = BoundedChannelFullMode.DropOldest,
        });

    public void Enqueue(AiPipelineRequest request) =>
        _channel.Writer.TryWrite(request);

    // Exposed to AiPipelineBackgroundService only — not part of the public interface.
    internal ChannelReader<AiPipelineRequest> Reader => _channel.Reader;
}
