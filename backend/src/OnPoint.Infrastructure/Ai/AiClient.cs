using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnPoint.Application.Ai;

namespace OnPoint.Infrastructure.Ai;

/// <summary>
/// Typed HTTP client that calls the Python AI microservice orchestrator endpoint.
/// Maps the Python snake_case response to the Application-layer AiPipelineResult.
/// Returns null on network failures so the orchestrator can apply graceful degradation.
/// </summary>
public sealed class AiClient : IAiService
{
    private readonly HttpClient _http;
    private readonly ILogger<AiClient> _logger;

    public AiClient(
        HttpClient http,
        IOptions<AiClientOptions> options,
        ILogger<AiClient> logger)
    {
        _http   = http;
        _logger = logger;

        var baseUrl = options.Value.BaseUrl.TrimEnd('/') + '/';
        _http.BaseAddress = new Uri(baseUrl);
        _http.Timeout     = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
    }

    public async Task<AiPipelineResult?> RunPipelineAsync(
        AiPipelineRequest request,
        CancellationToken ct = default)
    {
        var dto = new AiPipelineRunRequest
        {
            Text          = request.Text,
            BusinessId    = request.BusinessId,
            SessionId     = request.SessionId,
            IssueId       = request.IssueId,
            FeedbackId    = request.FeedbackId,
            Rating        = request.Rating,
            CorrelationId = request.CorrelationId,
        };

        AiPipelineRunResponse response;
        try
        {
            var httpResp = await _http.PostAsJsonAsync("api/v1/pipeline/run", dto, ct);
            httpResp.EnsureSuccessStatusCode();
            response = await httpResp.Content
                .ReadFromJsonAsync<AiPipelineRunResponse>(ct)
                ?? throw new InvalidOperationException("AI service returned null response body.");
        }
        catch (Exception ex) when (
            ex is HttpRequestException
                or TaskCanceledException
                or InvalidOperationException)
        {
            _logger.LogWarning(ex,
                "AI service unreachable for issue {IssueId}. " +
                "Graceful fallback will be applied (ai_fallback=true).",
                request.IssueId);
            return null;
        }

        return Map(response);
    }

    // ── Mapping ────────────────────────────────────────────────────────────────

    private static AiPipelineResult Map(AiPipelineRunResponse r)
    {
        var sentiment  = ToStageData(r.Sentiment);
        var classifier = ToStageData(r.Classifier);
        var priority   = ToStageData(r.Priority);
        var router     = ToStageData(r.Router);

        string? sentimentLabel = GetString(r.Sentiment.Output, "sentiment");
        double? urgencyScore   = GetDouble(r.Sentiment.Output, "urgency");

        string? category             = GetString(r.Classifier.Output, "category");
        double? classifierConfidence = r.Classifier.Confidence;

        int?    priorityScore      = GetInt(r.Priority.Output, "priority_score");
        string? priorityLabel      = GetString(r.Priority.Output, "priority_label");
        double? priorityConfidence = r.Priority.Confidence;

        string? departmentKey    = GetString(r.Router.Output, "department_key");
        double? routerConfidence = r.Router.Confidence;

        bool anyFallback =
            r.Sentiment.AiFallback  ||
            r.Classifier.AiFallback ||
            r.Priority.AiFallback   ||
            r.Router.AiFallback;

        return new AiPipelineResult(
            Sentiment:            sentiment,
            Classifier:           classifier,
            Priority:             priority,
            Router:               router,
            SentimentLabel:       sentimentLabel,
            UrgencyScore:         urgencyScore,
            Category:             category,
            ClassifierConfidence: classifierConfidence,
            PriorityScore:        priorityScore,
            PriorityLabel:        priorityLabel,
            PriorityConfidence:   priorityConfidence,
            DepartmentKey:        departmentKey,
            RouterConfidence:     routerConfidence,
            AnyFallback:          anyFallback,
            TotalLatencyMs:       r.TotalLatencyMs,
            TotalCostUsd:         r.TotalCostUsd
        );
    }

    private static AiStageData ToStageData(AiStagePredictionDto s) =>
        new(
            OutputJson:    JsonSerializer.Serialize(s.Output),
            Explanation:   s.Explanation,
            Confidence:    s.Confidence,
            Provider:      s.Provider,
            ModelVersion:  s.ModelVersion,
            PromptVersion: s.PromptVersion,
            LatencyMs:     s.LatencyMs,
            CostUsd:       s.CostUsd,
            AiFallback:    s.AiFallback,
            FallbackReason: s.FallbackReason
        );

    private static string? GetString(Dictionary<string, JsonElement> output, string key) =>
        output.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static double? GetDouble(Dictionary<string, JsonElement> output, string key)
    {
        if (!output.TryGetValue(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.Number ? el.GetDouble() : null;
    }

    private static int? GetInt(Dictionary<string, JsonElement> output, string key)
    {
        if (!output.TryGetValue(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.Number ? el.GetInt32() : null;
    }
}
