# Femur

> A strong foundation for scalable .NET applications.

Femur is a .NET utilities library designed to simplify application development at scale. Leaning into proven software design principles, it provides well-structured, reusable tooling that promotes maintainability, flexibility, and efficiency.

## Features

### Hosting & Application Lifecycle
- **ApplicationBuilder** - Fluent builder for console and web applications with structured lifecycle management
- **Exit Codes** - Standardized exit codes for operational integration
- **Error Handling** - Comprehensive error handling at each lifecycle stage (build, startup, runtime, shutdown)
- **Bootstrap Logging** - Log before host initialization for early diagnostics

### Configuration & Validation
- **IStandardOptions** - Fluent validation pattern for strongly-typed configuration
- **FluentValidation Integration** - Validate configuration at startup with detailed error messages
- **Convention-based Configuration** - Automatic configuration binding with validation

### Parsing & Markup
- **StreamParser** - Memory-efficient streaming parser base class for building custom parsers
- **HTML Parser** - HTML 2.0 compliant streaming parser
- **XML Parser** - Full XML document parsing with abstractions
- **Markdown Parser** - CommonMark 0.31.2 parser with extended syntax support (tables, footnotes, task lists)
- **Markdown Renderer** - Convert Markdown AST to HTML
- **CHTML** - Component-based HTML template language with C# code generation

### Messaging
- **Message Processing Framework** - Simple, opinionated framework with handler-based message processing
- **Transport Abstractions** - Clean abstractions supporting multiple message transports
- **In-Memory Transport** - Fast in-memory implementation for testing and local development
- **Azure Service Bus** - Production-ready Azure Service Bus transport implementation

### Infrastructure
- **Dependency Injection** - Cross-container service proxying and advanced DI patterns
- **FileSystem Abstractions** - Unified interface for local, in-memory, and Azure Blob storage
- **Serialization** - Async-first serialization framework with JSON support and extensibility
- **AspNetCore Utilities** - Endpoint building and web application integration

### Logging & Observability
- **Bootstrap Logger** - Early-stage logging before host fully initialized
- **OpenTelemetry Support** - Distributed tracing and observability integration

## Quick Start

```bash
# Install hosting package
dotnet add package Femur.Hosting

# Or install specific packages as needed
dotnet add package Femur.Markdown.Parser
dotnet add package Femur.Serialization
dotnet add package Femur.Messaging
```

Check out the [Getting Started Guide](docs/getting-started.md) for a complete walkthrough.

## Documentation

- [Getting Started](docs/getting-started.md) - 5-minute quickstart
- [Core Concepts](docs/core-concepts.md) - Architecture and design principles
- [Examples](docs/examples/index.md) - Complete working applications
- [Full Documentation](docs/README.md) - Comprehensive guides and API reference

## Examples

- **[API Data Aggregator](docs/examples/api-aggregator/README.md)** - Fetch and aggregate data from multiple APIs (demonstrates hosting, validation, logging, serialization)
- **[Logging Examples](src/Logging/examples/)** - Bootstrap logging patterns with OpenTelemetry integration
- **[Messaging Examples](src/Messaging/examples/)** - Message processing patterns with multiple transports
- **[CHTML Templates](src/Chtml/CSharpRenderer/examples/SimpleTemplates/)** - Component-based template examples

## Package Structure

Femur is organized into focused packages:

| Category | Packages |
|----------|----------|
| **Core** | Femur, Femur.Parsing, Femur.Markup.Abstractions |
| **Hosting** | Femur.Hosting, Femur.Hosting.Web, Femur.AspNetCore, Femur.AspNetCore.Endpoints |
| **Parsers** | Femur.Html.Parser, Femur.Xml.Parser, Femur.Markdown.Parser, Femur.Markdown.Renderer, Femur.Chtml.Parser |
| **Messaging** | Femur.Messaging, Femur.Messaging.InMemory, Femur.Messaging.ServiceBus |
| **Infrastructure** | Femur.DependencyInjection, Femur.FileSystem, Femur.FileSystem.AzureBlob, Femur.Serialization |
| **Logging** | Femur.Logging.Bootstrap |

See the [full package reference](docs/README.md#module-reference) for complete details.

## Contributing

Contributions are welcome! Please ensure code follows existing patterns and includes appropriate tests.

## License

MIT License - see LICENSE file for details.
