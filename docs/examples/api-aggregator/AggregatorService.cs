using System.Diagnostics;
using System.Text.Json;
using Femur.Hosting;
using Femur.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiAggregator;

/// <summary>
/// Main service that fetches data from multiple APIs and aggregates results.
/// Demonstrates IConsoleApplication pattern, bootstrap logging, and serialization.
/// </summary>
public class AggregatorService : IConsoleApplication
{
    private readonly ApiAggregatorOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAsyncSerializerFactory _serializerFactory;
    private readonly ILogger<AggregatorService> _logger;

    public AggregatorService(
        IOptions<ApiAggregatorOptions> options,
        IHttpClientFactory httpClientFactory,
        IAsyncSerializerFactory serializerFactory,
        ILogger<AggregatorService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _serializerFactory = serializerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the application logic. Returns exit code.
    /// This is the proper pattern for console apps that do work and exit.
    /// </summary>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting API aggregation for {Count} endpoints", _options.Endpoints.Count);

        try
        {
            var overallStopwatch = Stopwatch.StartNew();

            // Fetch data from all endpoints concurrently with throttling
            var results = await FetchAllEndpointsAsync(cancellationToken);

            overallStopwatch.Stop();

            // Create aggregated response
            var aggregated = new AggregatedResponse
            {
                Timestamp = DateTime.UtcNow,
                TotalEndpoints = _options.Endpoints.Count,
                SuccessfulRequests = results.Count(r => r.Success),
                FailedRequests = results.Count(r => !r.Success),
                TotalDuration = overallStopwatch.Elapsed,
                Results = results
            };

            // Serialize to JSON using IAsyncSerializerFactory
            await SerializeResultsAsync(aggregated, cancellationToken);

            _logger.LogInformation(
                "Aggregation complete: {Successful}/{Total} successful in {Duration:F2}s",
                aggregated.SuccessfulRequests,
                aggregated.TotalEndpoints,
                aggregated.TotalDuration.TotalSeconds);

            // Return success exit code
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("API aggregation was cancelled");
            return ExitCodes.CommandCancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during API aggregation");
            return ExitCodes.RuntimeError;
        }
    }

    /// <summary>
    /// Fetches data from all configured endpoints with concurrency throttling.
    /// </summary>
    private async Task<List<EndpointResult>> FetchAllEndpointsAsync(CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrentRequests);
        var tasks = _options.Endpoints.Select(endpoint => FetchEndpointAsync(endpoint, semaphore, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Fetches data from a single endpoint with error handling.
    /// </summary>
    private async Task<EndpointResult> FetchEndpointAsync(
        ApiEndpoint endpoint,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Fetching data from {Name}: {Url}", endpoint.Name, endpoint.Url);

            var stopwatch = Stopwatch.StartNew();
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            var response = await client.GetAsync(endpoint.Url, cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                // Parse response as JSON
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<JsonElement>(content);

                _logger.LogInformation(
                    "Successfully fetched {Name} ({StatusCode}) in {Duration:F2}s",
                    endpoint.Name,
                    (int)response.StatusCode,
                    stopwatch.Elapsed.TotalSeconds);

                return new EndpointResult
                {
                    Name = endpoint.Name,
                    Url = endpoint.Url,
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Duration = stopwatch.Elapsed,
                    Data = data
                };
            }
            else
            {
                _logger.LogWarning(
                    "Failed to fetch {Name}: HTTP {StatusCode}",
                    endpoint.Name,
                    (int)response.StatusCode);

                return new EndpointResult
                {
                    Name = endpoint.Name,
                    Url = endpoint.Url,
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    Duration = stopwatch.Elapsed,
                    ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching {Name}: {Message}", endpoint.Name, ex.Message);

            return new EndpointResult
            {
                Name = endpoint.Name,
                Url = endpoint.Url,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Serializes aggregated results to JSON file using IAsyncSerializerFactory.
    /// </summary>
    private async Task SerializeResultsAsync(AggregatedResponse response, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Writing results to {OutputFile}", _options.OutputFile);

            await using var stream = File.Create(_options.OutputFile);
            await _serializerFactory.SerializeAsync(stream, response, "application/json", cancellationToken);

            _logger.LogInformation("Results written to {OutputFile} ({Size} bytes)",
                _options.OutputFile,
                new FileInfo(_options.OutputFile).Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write output file: {Message}", ex.Message);
            throw;
        }
    }
}
