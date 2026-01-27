namespace ApiAggregator;

/// <summary>
/// Represents the aggregated response from multiple API endpoints.
/// </summary>
public class AggregatedResponse
{
    public DateTime Timestamp { get; set; }
    public int TotalEndpoints { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<EndpointResult> Results { get; set; } = new();
}

/// <summary>
/// Result from a single API endpoint.
/// </summary>
public class EndpointResult
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Data { get; set; }
}

/// <summary>
/// Configuration for a single API endpoint.
/// </summary>
public class ApiEndpoint
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";
}
