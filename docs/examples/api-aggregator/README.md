# API Data Aggregator Example

A complete console application demonstrating Femur's core features in a real-world scenario: fetching data from multiple APIs concurrently, validating configuration at startup, handling errors gracefully, and serializing results.

## What It Does

The API Data Aggregator:

1. **Loads configuration** from `appsettings.json` with environment-specific overrides
2. **Validates configuration** at startup using FluentValidation (fails fast if invalid)
3. **Fetches data** from multiple API endpoints concurrently with throttling
4. **Aggregates results** into a unified response structure
5. **Serializes output** to JSON using Femur's serialization framework
6. **Handles errors** gracefully at each lifecycle stage with appropriate exit codes
7. **Logs everything** using bootstrap logging (logs before host fully initialized)

## Features Demonstrated

### 1. ApplicationBuilder with Full Lifecycle

[Program.cs](Program.cs) demonstrates the staged fluent builder pattern:

```csharp
ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()           // Bootstrap logging
    .ConfigureConfiguration(...)          // Load config files
    .ConfigureServices(...)               // Register services
    .OnBuildError(...)                    // Error handling
    .OnRuntimeError(...)
    .RunAsync();                          // Build and run
```

Each stage only exposes methods valid for that phase, ensuring correct configuration order.

### 2. IStandardOptions with FluentValidation

[ApiAggregatorOptions.cs](ApiAggregatorOptions.cs) implements the `IStandardOptions<T>` pattern:

```csharp
public class ApiAggregatorOptions : IStandardOptions<ApiAggregatorOptions>
{
    public static string SectionName => "ApiAggregator";

    public List<ApiEndpoint> Endpoints { get; set; } = new();
    public string OutputFile { get; set; } = "output.json";
    public int TimeoutSeconds { get; set; } = 30;

    public static void SetupValidator(AbstractValidator<ApiAggregatorOptions> v)
    {
        v.RuleFor(x => x.Endpoints).NotEmpty();
        v.RuleFor(x => x.TimeoutSeconds).GreaterThan(0);
        v.RuleForEach(x => x.Endpoints)
            .Must(e => Uri.IsWellFormedUriString(e.Url, UriKind.Absolute));
    }
}
```

Validation runs during `ValidateOnStart()`, catching configuration errors before runtime.

### 3. Bootstrap Logging

The bootstrap logger (created by `UseDefaultConsoleLogging()`) logs:
- Configuration file loading
- Service registration
- Validation errors
- Application startup

All before the host is fully initialized, solving the "blind spot" problem.

### 4. IAsyncSerializer for JSON Output

[AggregatorService.cs](AggregatorService.cs#L135) uses `IAsyncSerializerFactory` for serialization:

```csharp
await using var stream = File.Create(options.OutputFile);
await _serializerFactory.SerializeAsync(stream, response, "application/json");
```

This abstraction allows swapping serialization formats (JSON, XML, etc.) without changing business logic.

### 5. Error Handling with Exit Codes

Different error handlers for different lifecycle phases:

| Handler | When It Triggers | Exit Code |
|---------|------------------|-----------|
| `OnBuildError` | Configuration/validation fails | 2 (BuildFailed) |
| `OnPreStartupError` | Host initialization fails | 4 (PreStartupError) |
| `OnRuntimeError` | Unhandled exception during execution | 3 (RuntimeError) |

Operational tooling (Docker, Kubernetes, CI/CD) can use these codes to understand failure reasons.

### 6. IConsoleApplication Pattern

Uses `IConsoleApplication` interface for proper console app lifecycle:

```csharp
public class AggregatorService : IConsoleApplication
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        // Do work
        return ExitCodes.Success;  // Return exit code
    }
}

// In Program.cs:
.RunAsync<AggregatorService>();
```

This is the correct pattern for console apps that execute a task and exit, unlike `IHostedService` which is designed for long-running background services.

### 7. HttpClient Integration

Standard .NET `HttpClient` with dependency injection:

```csharp
services.AddHttpClient();

// In service:
var client = _httpClientFactory.CreateClient();
```

Demonstrates integration with .NET ecosystem while using Femur's hosting framework.

## Running the Example

### Prerequisites

- .NET 9.0 SDK or later
- Internet connection (to fetch from JSONPlaceholder API)

### Steps

1. **Navigate to the example directory:**
   ```bash
   cd docs/examples/api-aggregator
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the project:**
   ```bash
   dotnet build
   ```

4. **Run the application:**
   ```bash
   dotnet run
   ```

Expected output:

```
info: ApiAggregator.AggregatorService[0]
      Starting API aggregation for 2 endpoints
info: ApiAggregator.AggregatorService[0]
      Successfully fetched JSONPlaceholder Posts (200) in 0.45s
info: ApiAggregator.AggregatorService[0]
      Successfully fetched JSONPlaceholder User (200) in 0.52s
info: ApiAggregator.AggregatorService[0]
      Writing results to output.json
info: ApiAggregator.AggregatorService[0]
      Results written to output.json (1234 bytes)
info: ApiAggregator.AggregatorService[0]
      Aggregation complete: 2/2 successful in 0.55s
```

5. **Check the output file:**
   ```bash
   cat output.json
   ```

You'll see aggregated results with status, timing, and data from each endpoint.

### Running in Development Environment

The `DOTNET_ENVIRONMENT` variable controls which configuration file is loaded:

```bash
# Uses appsettings.Development.json overrides
DOTNET_ENVIRONMENT=Development dotnet run

# Development mode enables:
# - More verbose logging (Debug level)
# - Different output file (output.dev.json)
# - Extended timeout (60 seconds)
```

## Configuration

The application is configured via `appsettings.json`:

```json
{
  "ApiAggregator": {
    "Endpoints": [
      {
        "Name": "JSONPlaceholder Posts",
        "Url": "https://jsonplaceholder.typicode.com/posts/1",
        "Description": "Sample API returning post data"
      }
    ],
    "OutputFile": "output.json",
    "TimeoutSeconds": 30,
    "MaxConcurrentRequests": 5
  }
}
```

### Configuration Options

| Option | Type | Description | Validation |
|--------|------|-------------|------------|
| `Endpoints` | Array | List of API endpoints to fetch | Must have at least one endpoint |
| `Endpoints[].Name` | String | Display name for the endpoint | Required |
| `Endpoints[].Url` | String | Full URL to fetch | Must be valid absolute URL |
| `Endpoints[].Description` | String | Optional description | None |
| `OutputFile` | String | Path to output JSON file | Required |
| `TimeoutSeconds` | Integer | HTTP request timeout | Must be > 0 |
| `MaxConcurrentRequests` | Integer | Max parallel requests | Must be 1-20 |

### Adding More Endpoints

Edit `appsettings.json` and add entries to the `Endpoints` array:

```json
{
  "Name": "GitHub API",
  "Url": "https://api.github.com/users/octocat",
  "Description": "GitHub user profile"
}
```

### Testing Validation

Try these configurations to see validation in action:

**Invalid URL:**
```json
{
  "Endpoints": [
    { "Name": "Invalid", "Url": "not-a-valid-url" }
  ]
}
```

Result: Application exits with validation error before runtime.

**Empty endpoints:**
```json
{
  "Endpoints": []
}
```

Result: "At least one API endpoint must be configured" error.

**Invalid timeout:**
```json
{
  "TimeoutSeconds": -5
}
```

Result: "Timeout must be greater than 0 seconds" error.

## Code Walkthrough

### Program.cs - Application Entry Point

The entry point demonstrates the complete ApplicationBuilder lifecycle:

1. **Create builder** with command-line args
2. **Bootstrap logging** setup (logs from this point forward)
3. **Configuration loading** from JSON files and environment
4. **Service registration** with validation
5. **Error handlers** for each phase
6. **Run** the application

Key insight: Errors at different phases get different handling and exit codes.

### ApiAggregatorOptions.cs - Configuration with Validation

Implements `IStandardOptions<T>` pattern:

- **SectionName**: Binds to `appsettings.json` section
- **Properties**: Strongly-typed configuration values
- **SetupValidator**: FluentValidation rules

Registration is one line:
```csharp
services.TryConfigureByConventionWithValidation<ApiAggregatorOptions>();
```

This automatically:
- Binds configuration section
- Registers validator
- Configures `ValidateOnStart()`

### AggregatorService.cs - Main Business Logic

Implements `IConsoleApplication` for console app execution:

- **ExecuteAsync**: Main application logic that runs once
  - Fetches all endpoints concurrently
  - Aggregates results
  - Serializes output
  - Returns appropriate exit code
  - Cancellation handled properly (returns `ExitCodes.CommandCancelled`)

Key patterns:
- **SemaphoreSlim** for concurrency throttling (respects `MaxConcurrentRequests`)
- **Stopwatch** for timing individual requests and overall duration
- **Try-catch** around each request for graceful error handling
- **IAsyncSerializerFactory** for content-type-based serialization

### ApiResponse.cs - Data Models

Three model classes:

1. **AggregatedResponse**: Top-level result with summary statistics
2. **EndpointResult**: Result from a single endpoint (success/failure, timing, data)
3. **ApiEndpoint**: Configuration for an endpoint (name, URL, description)

Models use nullable reference types (`string?`) for optional fields.

## Key Takeaways

After studying this example, you should understand:

1. **IConsoleApplication Pattern**: Proper way to build console apps (vs IHostedService anti-pattern)
2. **Staged Builder Pattern**: How ApplicationBuilder enforces configuration order
3. **Convention-based Configuration**: How `IStandardOptions<T>` simplifies config + validation
4. **Bootstrap Logging**: How to log before host initialization
5. **Error Handling Strategy**: Different handlers for different lifecycle phases
6. **Serialization Abstraction**: How `IAsyncSerializerFactory` decouples business logic from format
7. **Exit Codes**: How to communicate application state to operational tooling
8. **Validation at Startup**: How to fail fast with detailed error messages

## Extending the Example

### Add XML Output Support

1. Implement `IAsyncSerializer` for XML format
2. Register it with `services.AddSingleton<IAsyncSerializer, XmlSerializer>()`
3. Change content type in serialization call

### Add Retry Logic

Wrap HTTP calls with Polly for exponential backoff:

```csharp
services.AddHttpClient()
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
```

### Add Response Caching

Store responses in memory or Redis to avoid redundant API calls.

### Add Metrics

Integrate with `System.Diagnostics.Metrics` to export metrics:
- Request count per endpoint
- Success/failure rates
- Average response times

### Add OpenTelemetry

See the [Advanced Logging Example](../../../src/Logging/examples/AdvancedExample/) for distributed tracing integration.

## Related Examples

- **[Logging: Basic](../../../src/Logging/examples/BasicExample/)** - Simpler bootstrap logging example
- **[Logging: Advanced](../../../src/Logging/examples/AdvancedExample/)** - OpenTelemetry, Seq, Jaeger integration
- **[Getting Started](../../getting-started.md)** - Step-by-step Femur introduction

## Troubleshooting

### "At least one API endpoint must be configured"

Check `appsettings.json` has a non-empty `Endpoints` array.

### "Failed to fetch X: HTTP 403"

The API requires authentication. Use a different public API or add authentication headers.

### "Timeout waiting for response"

Increase `TimeoutSeconds` in configuration or check network connectivity.

### Application hangs

Check `MaxConcurrentRequests` - if set too low and endpoints are slow, it may appear to hang. Increase the value or reduce the number of endpoints.

## Next Steps

- Modify the configuration to fetch from your own APIs
- Add authentication headers for protected endpoints
- Implement response transformation logic
- Add unit tests using `InMemoryFileSystem` and mocked `HttpClient`
- Explore [Core Concepts](../../core-concepts.md) for deeper architecture understanding
