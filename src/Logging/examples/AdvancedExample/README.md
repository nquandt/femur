# Advanced Example

This example demonstrates advanced usage of LoggingBootstrap with OpenTelemetry integration:

## Features

### 1. **OpenTelemetry Integration**
- **Logging**: OpenTelemetry logging provider with structured logs
- **Tracing**: Distributed tracing with Activity API
  - Trace IDs and Span IDs for correlation
  - Parent-child span relationships
  - Custom tags and events
  - Status tracking (Ok, Error)
- **Metrics**: Runtime instrumentation for performance monitoring
- **Exporters**: Console exporters for demonstration (easily switch to OTLP)

### 2. **Comprehensive Error Handling**
- Try-catch blocks at the application level
- Specific exception types for different failure scenarios
- Proper error logging with exit codes
- Graceful degradation and recovery
- Error tracking in distributed traces

### 3. **Startup Validation**
- Environment validation (disk space, directories)
- Configuration validation (required settings)
- Service dependency validation
- Early failure detection before application runs

### 4. **Structured Logging & Observability**
- Semantic log levels (Debug, Information, Warning, Error, Critical)
- Structured data with log context
- Performance and health metrics logging
- Correlation of logs with traces via trace/span IDs
- Custom OpenTelemetry attributes and events

### 5. **Production-Ready Patterns**
- Configuration options using IOptions pattern
- Dependency injection with interfaces
- Multiple background services coordination
- Retry logic with exponential backoff
- Validation pipeline
- Distributed tracing throughout the request pipeline

### 6. **Graceful Shutdown**
- Proper cancellation token handling
- Resource cleanup in finally blocks
- Async disposal support

## Components

### WorkerService
Main service that:
- Generates work items continuously
- Validates items before processing
- Implements retry logic with configurable attempts
- Handles failures gracefully
- Creates distributed traces for each work item processing flow

### HealthCheckService
Monitoring service that:
- Reports memory usage
- Tracks thread pool metrics
- Runs periodic health checks

### WorkItemProcessor
Processing engine that:
- Simulates real-world processing delays
- Includes random failures for testing error handling
- Properly handles cancellation
- Tracks processing spans with OpenTelemetry
- Adds custom tags (delay_ms, result, error details)
- Records events (ProcessingStarted, ProcessingCompleted, ProcessingFailed)

### WorkItemValidator
Validation component that:
- Checks business rules
- Provides detailed error messages
- Can be enabled/disabled via configuration
- Creates validation spans with success/failure status
- Captures validation errors as span attributes

## OpenTelemetry Details

### Trace Hierarchy
Each work item creates a trace with the following span structure:
```
ProcessWorkItemWithRetries (parent span)
├── ValidateWorkItem (if validation enabled)
└── ProcessWorkItem (one per retry attempt)
```

### Custom Tags
- **workitem.id**: Work item identifier
- **workitem.description**: Work item description
- **validation.result**: success/failed
- **validation.error**: Error message if validation failed
- **processing.delay_ms**: Simulated processing delay
- **processing.result**: success/failed/cancelled
- **retry.max_attempts**: Maximum retry count
- **retry.current_attempt**: Current attempt number
- **retry.final_attempt**: Successful attempt number
- **error.type**: Exception type name
- **error.message**: Exception message

### Events
- **ProcessingStarted**: When processing begins
- **ProcessingCompleted**: On successful completion
- **ProcessingFailed**: On processing failure

## Running the Example

```bash
dotnet run --project examples/AdvancedExample
```

The application will:
1. Initialize bootstrap logging
2. Run startup validation checks
3. Configure and start services
4. Process work items with error handling
5. Report health metrics
6. Handle graceful shutdown on Ctrl+C

## Exit Codes

- `0`: Successful execution
- `1`: Configuration validation failed
- `2`: Startup validation failed
- `99`: Unhandled exception

## Configuration

### Worker Options
Modify `WorkerOptions` in Program.cs:
- `ProcessingInterval`: Time between work items
- `MaxRetries`: Number of retry attempts
- `EnableValidation`: Toggle validation pipeline

### OpenTelemetry Exporters
The example uses console exporters for demonstration. To export to a collector:

**Replace console exporters with OTLP:**
```csharp
// For Logging (in bootstrap logger configuration)
builder.AddOpenTelemetry(options =>
{
    // ... resource configuration ...
    options.AddOtlpExporter(); // Instead of AddConsoleExporter()
});

// For Tracing and Metrics (in main application)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("AdvancedExample")
        .AddOtlpExporter()) // Instead of AddConsoleExporter()
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddOtlpExporter()); // Instead of AddConsoleExporter()
```

**Configure OTLP endpoint** (via environment variables):
```bash
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
export OTEL_EXPORTER_OTLP_PROTOCOL="grpc"
```

Or in code:
```csharp
options.AddOtlpExporter(otlpOptions =>
{
    otlpOptions.Endpoint = new Uri("http://localhost:4317");
});
```

### Popular Observability Backends
- **Jaeger**: For distributed tracing
- **Prometheus + Grafana**: For metrics and dashboards
- **Elastic Stack**: For logs, metrics, and APM
- **Honeycomb**: SaaS observability platform
- **Datadog**: Full-stack monitoring
