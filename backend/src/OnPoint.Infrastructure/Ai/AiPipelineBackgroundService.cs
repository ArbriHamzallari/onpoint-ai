using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OnPoint.Infrastructure.Ai;

/// <summary>
/// Long-running hosted service that drains the AiPipelineQueue one item at a time
/// and invokes AiPipelineOrchestrator for each issue.
///
/// Single-item processing keeps the AI microservice from being overwhelmed during
/// bursts. If the orchestrator throws an unhandled exception, the error is logged
/// and the service continues — one bad item does not kill the queue.
/// </summary>
public sealed class AiPipelineBackgroundService : BackgroundService
{
    private readonly AiPipelineQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiPipelineBackgroundService> _logger;

    public AiPipelineBackgroundService(
        AiPipelineQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AiPipelineBackgroundService> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI pipeline background service started.");

        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var orchestrator = scope.ServiceProvider
                    .GetRequiredService<AiPipelineOrchestrator>();

                await orchestrator.ProcessAsync(request, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Unhandled error processing AI pipeline for issue {IssueId}. " +
                    "Service continues — item dropped.",
                    request.IssueId);
            }
        }

        _logger.LogInformation("AI pipeline background service stopping.");
    }
}
