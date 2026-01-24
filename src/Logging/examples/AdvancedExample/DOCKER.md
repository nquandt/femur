# Running AdvancedExample with Docker Compose and OpenTelemetry

This guide explains how to run the AdvancedExample with a complete observability stack using Docker Compose.

## Architecture

The Docker Compose setup includes:

- **Application**: .NET application with OpenTelemetry instrumentation
- **OpenTelemetry Collector**: Receives telemetry data via OTLP protocol
- **Seq**: Simple, beautiful log viewer (recommended starting point!)
- **Jaeger**: Distributed tracing backend and UI
- **Prometheus**: Metrics storage and querying
- **Grafana**: Unified visualization dashboard

```
┌─────────────┐
│   .NET App  │ ──OTLP──┐
└─────────────┘          │
                         ▼
                ┌────────────────┐
                │ OTEL Collector │
                └────────────────┘
              Logs │   Traces │   Metrics
                   ▼          ▼          ▼
              ┌─────┐   ┌─────────┐   ┌────────────┐
              │ Seq │   │ Jaeger  │   │ Prometheus │
              └─────┘   └─────────┘   └────────────┘
                             │              │
                             └──────┬───────┘
                                    ▼
                              ┌──────────┐
                              │ Grafana  │
                              └──────────┘
```

## Prerequisites

- Docker and Docker Compose installed
- At least 4GB of available memory for Docker

## Quick Start

1. **Navigate to the example directory:**
   ```bash
   cd src/Logging/examples/AdvancedExample
   ```

2. **Start all services:**
   ```bash
   docker compose up --build
   ```

   This will:
   - Build the .NET application
   - Start the OpenTelemetry Collector
   - Start Jaeger, Prometheus, and Grafana
   - Run the application with OTLP exporters

3. **Access the observability tools:**

   - **Seq** (Logs): http://localhost:5341 - **Start here!**
   - **Jaeger** (Traces): http://localhost:16686
   - **Prometheus** (Metrics): http://localhost:9090
   - **Grafana** (Dashboards): http://localhost:3000
     - Username: `admin`
     - Password: `admin`

## What to Observe

### Logs in Seq (Start Here!)

**Seq** provides the simplest way to view all your application logs with a clean, searchable interface.

1. Open http://localhost:5341
2. You'll immediately see all logs flowing in real-time
3. Features you'll love:
   - **Live tail**: Logs appear instantly as they're generated
   - **Structured properties**: Click any property to filter (e.g., click "Error" to see only errors)
   - **Full-text search**: Search through all log messages
   - **TraceId correlation**: Click on a TraceId to see all logs for that trace
   - **Log levels**: Color-coded by severity (Debug, Info, Warning, Error)
   - **Time navigation**: Jump to specific time ranges
   - **SQL-like queries**: Advanced filtering with structured queries

**Quick Tips:**
- Click the "📊 Signal" in any log entry to see its properties
- Use the search bar: `@Level = 'Error'` to see only errors
- Click on a TraceId to filter logs for a specific operation
- Use the time picker in the top right to navigate through history
- Click "Stream" to enable/disable live updates

**Example queries:**
```sql
-- All error logs
@Level = 'Error'

-- Logs from WorkItemProcessor
SourceContext like '%WorkItemProcessor%'

-- Logs with a specific TraceId (replace with actual ID)
TraceId = '80d49d8b5e7c9d4e1a2f3b4c5d6e7f8a'

-- Processing times over 100ms
Properties.processing_delay_ms > 100
```

### Traces in Jaeger

1. Open http://localhost:16686
2. Select "AdvancedExample" from the Service dropdown
3. Click "Find Traces"
4. You'll see traces from:
   - `WorkerService` processing work items
   - `WorkItemProcessor.ProcessAsync` operations
   - `WorkItemValidator.ValidateAsync` validations
   - `HealthCheckService` health checks

### Metrics in Prometheus

1. Open http://localhost:9090
2. Try these queries:
   ```promql
   # Runtime metrics
   femur_process_runtime_dotnet_gc_collections_count_total
   femur_process_runtime_dotnet_gc_heap_size_bytes

   # All available metrics from the app
   {job="otel-collector"}
   ```

### Unified View in Grafana

1. Open http://localhost:3000
2. Login with admin/admin
3. Navigate to Explore
4. Select "Jaeger" datasource to view traces
5. Select "Prometheus" datasource to view metrics

### Correlating Logs and Traces

One of the most powerful features is seeing logs and traces together:

**From Seq to Jaeger:**
1. In Seq, find an interesting log entry
2. Click on the TraceId property (looks like `80d49d8b5e7c9d4e1a2f3b4c5d6e7f8a`)
3. Copy the TraceId value
4. Open Jaeger (http://localhost:16686)
5. Paste the TraceId in the "Trace ID" search box
6. See the complete distributed trace showing timing and flow

**From Jaeger to Seq:**
1. In Jaeger, find an interesting trace
2. Copy the Trace ID from the top
3. Open Seq (http://localhost:5341)
4. Search: `TraceId = 'paste-trace-id-here'`
5. See all the detailed logs for that specific trace

This two-way correlation makes debugging dramatically easier!

## Application Behavior

The AdvancedExample demonstrates:

- **Bootstrap logging** during startup with OTLP export
- **Distributed tracing** with correlated activities
- **Runtime metrics** (GC, memory, thread pools)
- **Structured logging** with OpenTelemetry
- **Error handling** with span events
- **Validation workflows** with nested spans

### Processing Flow

1. `WorkerService` wakes up every 3 seconds
2. Creates a `WorkItem` with random data
3. Validates the item (creates a span)
4. Processes the item (creates a span with custom attributes)
5. All operations are traced and metrics are collected

## Stopping the Services

Stop all services and remove containers:
```bash
docker compose down
```

Remove volumes (clears all data):
```bash
docker compose down -v
```

## Configuration

### OTLP Endpoint

The application is configured to use OTLP via environment variables:

```yaml
environment:
  - OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4318
  - OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

### OpenTelemetry Collector

Configuration: [otel-collector-config.yaml](otel-collector-config.yaml)

The collector:
- Receives OTLP data on ports 4317 (gRPC) and 4318 (HTTP)
- Batches telemetry data for efficiency
- Exports logs to Seq
- Exports traces to Jaeger
- Exports metrics to Prometheus
- Logs telemetry to console for debugging

### Prometheus

Configuration: [prometheus.yml](prometheus.yml)

Scrapes metrics from:
- OpenTelemetry Collector metrics endpoint (port 8889)
- Collector's own internal metrics (port 8888)

### Grafana

Datasources are pre-configured via provisioning:
- Prometheus datasource for metrics
- Jaeger datasource for traces

## Local Development (Without Docker)

The application automatically falls back to console exporters when `OTEL_EXPORTER_OTLP_ENDPOINT` is not set:

```bash
cd src/Logging/examples/AdvancedExample
dotnet run
```

This runs the application with console exporters for quick local testing.

## Troubleshooting

### Application not sending telemetry

Check the OpenTelemetry Collector logs:
```bash
docker compose logs otel-collector
```

### Collector not exporting to Jaeger

Verify Jaeger is running:
```bash
docker compose ps jaeger
```

Check collector configuration:
```bash
docker compose exec otel-collector cat /etc/otel-collector-config.yaml
```

### No metrics in Prometheus

1. Check if Prometheus is scraping the collector:
   - Open http://localhost:9090/targets
   - Verify "otel-collector" targets are "UP"

2. Check collector metrics endpoint:
   ```bash
   curl http://localhost:8889/metrics
   ```

### Port conflicts

If ports are already in use, modify the port mappings in `docker-compose.yml`:

```yaml
ports:
  - "3001:3000"  # Grafana on different port
```

## Performance Notes

### Resource Usage

Expected resource usage:
- Application: ~200 MB RAM
- OpenTelemetry Collector: ~100 MB RAM
- Jaeger: ~400 MB RAM
- Prometheus: ~200 MB RAM
- Grafana: ~150 MB RAM

Total: ~1 GB RAM

### Data Retention

- **Jaeger**: In-memory storage (data lost on restart)
- **Prometheus**: 15-day retention (configurable)
- **Grafana**: Persistent dashboards and settings

For production, consider:
- Persistent storage for Jaeger (Elasticsearch, Cassandra)
- Longer retention for Prometheus
- External authentication for Grafana

## Next Steps

1. **Create custom dashboards** in Grafana
2. **Add alerts** in Prometheus
3. **Instrument additional code** with OpenTelemetry
4. **Export to cloud backends** (Application Insights, Datadog, etc.)

## References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [OTLP Specification](https://opentelemetry.io/docs/specs/otlp/)
