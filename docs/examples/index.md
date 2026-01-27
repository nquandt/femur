# Femur Examples

Complete working applications demonstrating Femur features in realistic scenarios.

## API Data Aggregator

**Location:** [api-aggregator/README.md](api-aggregator/README.md)

**What it does:** Fetches data from multiple APIs concurrently, aggregates results, validates configuration at startup, handles errors gracefully, and serializes output to JSON.

**Features Demonstrated:**
- ApplicationBuilder with full lifecycle management
- IStandardOptions with FluentValidation at startup
- Bootstrap logging for early diagnostics
- IAsyncSerializer for JSON output
- Error handling with exit codes
- HttpClient integration with dependency injection

**Best for:** Learning how to integrate multiple Femur features in a real-world console application.

**Complexity:** Intermediate

---

## Logging Examples

Examples demonstrating bootstrap logging patterns and observability integration.

### Basic Example

**Location:** [../../src/Logging/examples/BasicExample/README.md](../../src/Logging/examples/BasicExample/)

**What it does:** Simple console application with bootstrap logging and hosted service.

**Features Demonstrated:**
- Bootstrap logging setup
- Console logging configuration
- IHostedService integration
- Basic ApplicationBuilder usage

**Best for:** Understanding bootstrap logging fundamentals.

**Complexity:** Beginner

### Advanced Example

**Location:** [../../src/Logging/examples/AdvancedExample/README.md](../../src/Logging/examples/AdvancedExample/)

**What it does:** Production-ready console application with OpenTelemetry integration, distributed tracing, structured logging, and observability stack (Seq, Jaeger, Prometheus, Grafana).

**Features Demonstrated:**
- Bootstrap logging with OpenTelemetry
- Distributed tracing with Activity and ActivitySource
- Structured logging with scopes
- Error handling and validation
- Docker Compose observability stack
- OTLP exporter configuration
- Console and remote logging destinations

**Best for:** Production-grade logging and observability patterns.

**Complexity:** Advanced

---

## CHTML Template Example

**Location:** [../../src/Chtml/CSharpRenderer/examples/SimpleTemplates/README.md](../../src/Chtml/CSharpRenderer/examples/SimpleTemplates/)

**What it does:** Component-based HTML template project with dynamic routing, C# code generation, and reusable components.

**Features Demonstrated:**
- CHTML component syntax
- C# code generation from templates
- Component composition and reuse
- Dynamic content rendering
- Project structure for template-based applications

**Best for:** Understanding component-based templating and compile-time template processing.

**Complexity:** Intermediate

---

## Example Comparison

| Example | Focus Area | Complexity | Key Features |
|---------|------------|------------|--------------|
| **API Data Aggregator** | Hosting, Validation, Serialization | Intermediate | ApplicationBuilder, IStandardOptions, IAsyncSerializer, Exit Codes |
| **Logging: Basic** | Bootstrap Logging | Beginner | BootstrapLogger, Console output, IHostedService |
| **Logging: Advanced** | Observability | Advanced | OpenTelemetry, Distributed tracing, Seq/Jaeger/Grafana |
| **CHTML Templates** | Component Templates | Intermediate | CHTML parsing, C# generation, Component model |

## Running the Examples

Each example includes a README with:
- **What it demonstrates** - Key features and concepts
- **How to run it** - Step-by-step instructions
- **Configuration** - Settings and customization options
- **Code walkthrough** - Explanation of implementation details
- **Key takeaways** - What you should learn

### General Steps

1. Navigate to the example directory
2. Restore dependencies: `dotnet restore`
3. Build the project: `dotnet build`
4. Run the application: `dotnet run`

Some examples (like Advanced Logging) require Docker Compose for observability infrastructure:

```bash
cd src/Logging/examples/AdvancedExample
docker compose up -d  # Start Seq, Jaeger, Prometheus, Grafana
dotnet run
```

## Creating Your Own Application

After exploring these examples, use them as templates for your own applications:

1. **Start with API Data Aggregator** if you need:
   - Console application with configuration
   - Validated options
   - HTTP client integration
   - JSON serialization

2. **Start with Logging: Basic** if you need:
   - Simple console app
   - Bootstrap logging
   - Minimal dependencies

3. **Start with Logging: Advanced** if you need:
   - Production observability
   - Distributed tracing
   - OpenTelemetry integration
   - Structured logging

4. **Start with CHTML Templates** if you need:
   - Component-based HTML generation
   - Compile-time template processing
   - Type-safe templating

## Additional Resources

- [Getting Started Guide](../getting-started.md) - Build your first Femur application from scratch
- [Core Concepts](../core-concepts.md) - Understand Femur's architecture and design principles
- [Module Reference](../README.md#module-reference) - Explore all available packages

## Contributing Examples

Have you built something interesting with Femur? Consider contributing an example:

1. Create a focused example demonstrating 2-3 features
2. Include a comprehensive README explaining what it does and why
3. Ensure it compiles and runs without errors
4. Add it to this index

Examples help the community learn and showcase Femur's capabilities!
