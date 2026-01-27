# Femur Documentation

> A strong foundation for scalable .NET applications.

## Quick Navigation

- **[Getting Started](getting-started.md)** - 5-minute quickstart guide
- **[Core Concepts](core-concepts.md)** - Architecture and design principles
- **[Examples](examples/index.md)** - Complete working applications

## Common Tasks

- **[Building Console Applications](#hosting--applications)** - Use ApplicationBuilder for structured lifecycle management
- **[Validating Configuration](#core)** - Implement IStandardOptions with FluentValidation at startup
- **[Parsing Content](#parsing--markup)** - Use StreamParser base class or specialized parsers (HTML, XML, Markdown)
- **[Logging Before Host Startup](#logging--observability)** - Use BootstrapLogger for early diagnostics
- **[Serializing Objects](#infrastructure)** - Use IAsyncSerializer for JSON and custom formats

## Module Reference

### Core

| Package | Description |
|---------|-------------|
| **Femur** | Core utilities including IStandardOptions, FluentValidationOptions, and service collection extensions for convention-based configuration validation |
| **Femur.Parsing** | StreamParser<TDocument> base class for building memory-efficient streaming parsers with AST support |
| **Femur.Markup.Abstractions** | Shared abstractions for markup languages including DocumentNode, ElementNode, TextNode, CommentNode, and common node types |

### Hosting & Applications

| Package | Description |
|---------|-------------|
| **Femur.Hosting** | ApplicationBuilder with fluent API for console applications, lifecycle management, exit codes, and comprehensive error handling across build/startup/runtime/shutdown phases |
| **Femur.Hosting.Web** | WebApplicationBuilder for ASP.NET Core applications with similar fluent patterns and lifecycle management |
| **Femur.AspNetCore** | Core ASP.NET Core utilities and integration helpers |
| **Femur.AspNetCore.Endpoints** | Endpoint routing utilities and building blocks for minimal APIs |

### Parsing & Markup

| Package | Description |
|---------|-------------|
| **Femur.Html.Parser** | HTML 2.0 compliant streaming parser with comprehensive element and attribute support |
| **Femur.Xml.Parser** | XML document parser with streaming capabilities |
| **Femur.Xml.Abstractions** | XML-specific node abstractions and utilities |
| **Femur.Markdown.Parser** | CommonMark 0.31.2 compliant streaming parser producing complete AST |
| **Femur.Markdown.Abstractions** | Markdown node types including BlockNode, InlineNode, and specialized elements |
| **Femur.Markdown.Renderer** | MarkdownHtmlRenderer for converting Markdown AST to HTML output |
| **Femur.Markdown.Extended.Parser** | Extended Markdown syntax parser supporting tables, footnotes, task lists, and fenced divs |
| **Femur.Markdown.Extended.Abstractions** | Abstractions for extended Markdown features |
| **Femur.Chtml.Parser** | Component HTML parser for component-based template language |
| **Femur.Chtml.CSharpRenderer** | C# code generator for CHTML templates enabling compile-time checking |
| **Femur.Chtml.Runtime** | Runtime support for executing CHTML components |

### Infrastructure

| Package | Description |
|---------|-------------|
| **Femur.DependencyInjection** | ProxiedServiceCollectionExtensions for cross-container service sharing while preserving lifetimes and factory functions |
| **Femur.FileSystem** | IFileSystem and IReadonlyFileSystem abstractions with DefaultFileSystem (local) and InMemoryFileSystem implementations |
| **Femur.FileSystem.AzureBlob** | AzureBlobStorageFileSystem implementation for cloud storage |
| **Femur.Serialization** | IAsyncSerializer framework with DefaultJsonSerializer, content-type-based factory pattern, and extensibility for custom formats |

### Logging & Observability

| Package | Description |
|---------|-------------|
| **Femur.Logging.Bootstrap** | BootstrapLogger for logging during application configuration phase before host fully initialized, supports OpenTelemetry integration |

## Package Categories

### 1. Hosting & Applications

Build structured console and web applications with comprehensive lifecycle management:

- **ApplicationBuilder** provides fluent API with staged configuration (bootstrap → configuration → services → execution)
- **Exit codes** for operational integration (success, build failures, runtime errors, etc.)
- **Error handling** at each phase with custom handlers for builder, build, pre-startup, runtime, and post-shutdown errors
- **Bootstrap logging integration** for early diagnostics

**Key interfaces:** `IConsoleApplication`, `IBootstrapApplicationBuilder`, `IConfigurationApplicationBuilder`

### 2. Parsing & Markup

Parse and process HTML, XML, Markdown, and component-based templates:

- **StreamParser pattern** for memory-efficient processing of large files
- **AST-based parsing** with complete node hierarchy and position tracking
- **Multiple markup languages** with consistent abstractions
- **Rendering support** for transforming parsed content to output formats

**Key classes:** `StreamParser<TDocument>`, `MarkdownHtmlRenderer`, `HtmlParser`

### 3. Infrastructure

Cross-cutting concerns for application development:

- **Dependency Injection** utilities for advanced scenarios (cross-container proxying)
- **File System abstractions** supporting local, in-memory, and cloud storage
- **Serialization framework** with async-first design and content-type negotiation

**Key interfaces:** `IFileSystem`, `IAsyncSerializer`, `IAsyncSerializerFactory`

### 4. Configuration & Validation

Strongly-typed configuration with validation:

- **IStandardOptions pattern** for convention-based configuration
- **FluentValidation integration** with startup validation
- **Detailed error messages** for configuration problems
- **Configuration change monitoring** with IOptionsChangeTokenSource

**Key interfaces:** `IStandardOptions<TOptions>`, `FluentValidationOptions<TOptions>`

### 5. Logging & Observability

Comprehensive logging from application start to finish:

- **Bootstrap logging** before host initialization
- **OpenTelemetry support** for distributed tracing
- **Activity source integration** for observability
- **Shared logging providers** between bootstrap and host phases

**Key classes:** `BootstrapLogger`, logging examples with Seq/Jaeger/Grafana integration

## Design Patterns

Femur leverages proven software design patterns:

| Pattern | Where Used | Purpose |
|---------|------------|---------|
| **Fluent Builder** | ApplicationBuilder, WebApplicationBuilder | Staged configuration with type-safe method chaining |
| **Template Method** | StreamParser<TDocument> | Customizable parsing algorithm with fixed structure |
| **Factory** | IAsyncSerializerFactory | Content-type-based serializer resolution |
| **Options** | IStandardOptions<TOptions> | Strongly-typed configuration with validation |
| **Repository** | IFileSystem | Abstract storage operations across implementations |
| **Strategy** | IAsyncSerializer | Pluggable serialization strategies |

## Cross-Cutting Features

- **Async/await first** - All I/O operations fully async
- **Dependency injection** - Built on Microsoft.Extensions.DependencyInjection
- **Interface-based design** - Testable and flexible
- **Multi-targeting** - netstandard2.0, net8.0, net9.0, net10.0 support
- **Memory efficiency** - Streaming parsers, buffer pooling, minimal allocations

## See Also

- [Getting Started Guide](getting-started.md) - Build your first Femur application
- [Core Concepts](core-concepts.md) - Understand the architecture
- [API Data Aggregator Example](examples/api-aggregator/README.md) - Complete working application
- [Logging Examples](../src/Logging/examples/) - Bootstrap logging patterns
- [GitHub Repository](https://github.com/nquandt/femur) - Source code and issues
