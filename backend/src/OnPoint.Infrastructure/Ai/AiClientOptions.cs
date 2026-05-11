namespace OnPoint.Infrastructure.Ai;

public sealed class AiClientOptions
{
    /// <summary>Base URL of the Python AI microservice (e.g. http://localhost:5200).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5200";

    /// <summary>HTTP timeout per pipeline call in seconds. Default 10s.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
