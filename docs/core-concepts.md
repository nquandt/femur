# Core Concepts

This document explains Femur's architecture, design principles, and the philosophy behind its implementation.

## Design Principles

Femur is built on four core principles that guide its design and implementation:

### 1. Fluent Validation at Startup

Configuration errors should be caught as early as possible, ideally before the application fully starts.

**Why:** In production environments, an application that starts successfully but fails later due to invalid configuration is harder to diagnose and causes more downtime than one that fails fast at startup.

**How:** The `IStandardOptions<TOptions>` pattern integrates FluentValidation with .NET's Options pattern, running validation during the build phase via `ValidateOnStart()`. If configuration is invalid, the application never enters the runtime phase.

**Example:**
```csharp
public class DatabaseOptions : IStandardOptions<DatabaseOptions>
{
    public static string SectionName => "Database";
    public string ConnectionString { get; set; } = "";

    public static void SetupValidator(AbstractValidator<DatabaseOptions> v)
    {
        v.RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .Must(cs => cs.Contains("Server="))
            .WithMessage("Connection string must contain Server parameter");
    }
}
```

If `appsettings.json` is missing the `ConnectionString` or it's malformed, the application exits with `ExitCodes.BuildFailed` (2) and a detailed validation message before any runtime code executes.

### 2. Bootstrap Logging for Early Diagnostics

The "blind spot" in traditional .NET Generic Host applications is the configuration phase—logging isn't available until after configuration is loaded and services are registered.

**Why:** When an application fails during configuration loading or service registration, developers have no visibility into what went wrong. Bootstrap logging illuminates this blind spot.

**How:** `BootstrapLogger` creates a lightweight, standalone logging infrastructure before the host is built. It can log configuration loading, validation errors, and service registration, then seamlessly transfers to the main host.

**Example:**
```csharp
await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()  // Creates BootstrapLogger
    .ConfigureConfiguration((context, config) =>
    {
        // Logs: "Loading appsettings.json"
        config.AddJsonFile("appsettings.json");
    })
    .ConfigureServices((context, services) =>
    {
        // Logs: "Registering services..."
        services.TryConfigureByConventionWithValidation<AppSettings>();
        // Logs validation errors if configuration is invalid
    })
    .RunAsync();
```

The same console output (or OpenTelemetry exporter) receives logs from both bootstrap and runtime phases.

### 3. Graceful Error Handling with Exit Codes

Applications should handle errors at appropriate lifecycle stages and communicate failure reasons via standardized exit codes.

**Why:** Operational tooling (Kubernetes, Docker, CI/CD pipelines, monitoring systems) relies on exit codes to understand whether an application started successfully, failed during startup, or crashed at runtime.

**How:** `ApplicationBuilder` differentiates errors by lifecycle phase and provides specific error handlers for each:

- **OnBuilderError**: Before host builder is created
- **OnBuildError**: During configuration/services registration
- **OnPreStartupError**: During `host.RunAsync()` initialization
- **OnRuntimeError**: During normal execution
- **OnPostShutdownError**: During disposal/cleanup

Each handler can log appropriately and return a specific exit code.

**Example:**
```csharp
await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices(...)
    .OnBuildError((ex, logger) =>
    {
        logger.LogCritical(ex, "Configuration validation failed");
        return ExitCodes.BuildFailed;  // Exit code 2
    })
    .OnRuntimeError((ex, logger) =>
    {
        logger.LogCritical(ex, "Unhandled exception in application");
        return ExitCodes.RuntimeError;  // Exit code 3
    })
    .RunAsync();
```

### 4. Streaming Parsers for Memory Efficiency

Large files (multi-MB HTML documents, extensive Markdown files, XML feeds) should be parsed without loading the entire file into memory.

**Why:** Loading a 50MB XML file into a string consumes at least 100MB of memory (UTF-16 encoding doubles size). For long-running services or resource-constrained environments, this is wasteful.

**How:** `StreamParser<TDocument>` implements a sliding buffer approach: it reads chunks (default 4KB) from a stream, processes characters one-by-one, and builds an Abstract Syntax Tree (AST) incrementally. Only the AST (much smaller than raw text) is kept in memory.

**Example:**
```csharp
using var stream = File.OpenRead("large-document.html");
var parser = new HtmlParser();
var document = parser.Parse(stream);  // Streams through file

// Memory usage: ~4KB buffer + AST size (10-20% of file size)
// vs. loading entire file: 100% of file size * 2 (UTF-16)
```

All Femur parsers (HTML, XML, Markdown, CHTML) use this pattern.

## Package Categories

Femur's 25+ packages are organized into five categories based on their primary purpose:

### 1. Core (Femur, Femur.Parsing, Femur.Markup.Abstractions)

Foundational abstractions and base classes:
- **Femur**: Options pattern, validation integration, service collection extensions
- **Femur.Parsing**: `StreamParser<TDocument>` base class, node abstractions
- **Femur.Markup.Abstractions**: Common node types for markup languages (ElementNode, TextNode, etc.)

### 2. Hosting & Applications (Femur.Hosting, Femur.Hosting.Web, Femur.AspNetCore)

Application lifecycle management:
- **Femur.Hosting**: Console application builder with staged configuration
- **Femur.Hosting.Web**: ASP.NET Core web application builder
- **Femur.AspNetCore**: ASP.NET utilities and endpoint helpers

### 3. Parsing & Markup (Femur.Html.Parser, Femur.Xml.Parser, Femur.Markdown.*, Femur.Chtml.*)

Content parsing and rendering:
- **HTML/XML Parsers**: Standards-compliant parsers for web content
- **Markdown**: CommonMark parser, renderer, and extended syntax support
- **CHTML**: Component-based template language with C# code generation

### 4. Infrastructure (Femur.DependencyInjection, Femur.FileSystem, Femur.Serialization)

Cross-cutting infrastructure:
- **DI**: Advanced dependency injection patterns (cross-container proxying)
- **FileSystem**: Storage abstractions (local, in-memory, Azure Blob)
- **Serialization**: Async serialization framework with content-type negotiation

### 5. Logging & Observability (Femur.Logging.Bootstrap)

Diagnostic infrastructure:
- **Bootstrap Logging**: Pre-host logging with OpenTelemetry support

## Architecture Patterns

Femur leverages proven Gang of Four (GoF) and enterprise patterns:

### Fluent Builder Pattern

**Where:** ApplicationBuilder, WebApplicationBuilder

**Why:** Staged configuration ensures developers configure the application in the correct order (bootstrap → configuration → services → execution) while maintaining type safety.

**How:** Each builder stage returns a different interface:

```csharp
IInitialApplicationBuilder           // Create(args)
    ↓
IBootstrapApplicationBuilder         // UseDefaultConsoleLogging()
    ↓
IConfigurationApplicationBuilder     // ConfigureConfiguration()
    ↓
IServicesApplicationBuilder          // ConfigureServices()
    ↓
IExecutableApplicationBuilder        // RunAsync()
```

Each interface only exposes methods valid for that stage, preventing misuse.

### Template Method Pattern

**Where:** StreamParser<TDocument>

**Why:** Parsing algorithms share common structure (read buffer → process character → build AST → cleanup) but differ in specifics (how to recognize elements, how to build nodes).

**How:** `StreamParser<TDocument>` provides the algorithm skeleton and calls abstract methods that subclasses implement:

```csharp
public abstract class StreamParser<TDocument>
{
    public TDocument Parse(Stream stream)  // Template method
    {
        var doc = CreateDocument();         // Abstract
        InitializeParsing();                // Abstract

        while (ReadMore())
        {
            ProcessCharacter(CurrentChar);  // Abstract
        }

        Cleanup();                          // Abstract
        return doc;
    }

    protected abstract TDocument CreateDocument();
    protected abstract void ProcessCharacter(char c);
    // etc.
}
```

Subclasses like `HtmlParser`, `MarkdownParser` implement these methods.

### Factory Pattern

**Where:** IAsyncSerializerFactory

**Why:** Serialization format should be chosen at runtime based on content type (application/json, application/xml, etc.).

**How:** `IAsyncSerializerFactory` maps content types to serializer implementations:

```csharp
var factory = serviceProvider.GetRequiredService<IAsyncSerializerFactory>();

// Resolves to DefaultJsonSerializer
await factory.SerializeAsync(stream, obj, "application/json");

// Could resolve to XmlSerializer (if registered)
await factory.SerializeAsync(stream, obj, "application/xml");
```

New serializers register themselves and are automatically available.

### Options Pattern

**Where:** IStandardOptions<TOptions>

**Why:** .NET's built-in `IOptions<T>` provides configuration binding but lacks convention-based validation integration.

**How:** `IStandardOptions<TOptions>` extends the pattern with:
- **SectionName**: Convention for configuration section binding
- **SetupValidator**: Declarative validation rules
- **TryConfigureByConventionWithValidation()**: One-line registration

```csharp
services.TryConfigureByConventionWithValidation<DatabaseOptions>();

// Equivalent to:
services.Configure<DatabaseOptions>(config.GetSection("Database"));
services.AddSingleton<IValidateOptions<DatabaseOptions>, FluentValidationOptions<DatabaseOptions>>();
services.AddSingleton<AbstractValidator<DatabaseOptions>>(new DefaultValidator<DatabaseOptions>());
```

### Repository Pattern

**Where:** IFileSystem, IReadonlyFileSystem

**Why:** Storage location (local disk, Azure Blob, in-memory) should be swappable without changing business logic.

**How:** `IFileSystem` abstracts file operations:

```csharp
public interface IFileSystem
{
    Task<string> ReadFileAsync(string path, CancellationToken ct = default);
    Task WriteFileAsync(string path, string content, CancellationToken ct = default);
    Task DeleteFileAsync(string path, CancellationToken ct = default);
    // etc.
}
```

Implementations:
- `DefaultFileSystem`: Uses `System.IO`
- `InMemoryFileSystem`: Dictionary<string, byte[]>
- `AzureBlobStorageFileSystem`: Azure SDK

Business logic depends on `IFileSystem`, tests inject `InMemoryFileSystem`, production uses `DefaultFileSystem` or `AzureBlobStorageFileSystem`.

### Strategy Pattern

**Where:** IAsyncSerializer

**Why:** Serialization algorithm varies by format (JSON, XML, MessagePack, Protocol Buffers).

**How:** `IAsyncSerializer` defines the interface, implementations provide the algorithm:

```csharp
public interface IAsyncSerializer
{
    string[] ContentTypes { get; }
    Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken ct) where T : class;
    Task SerializeAsync<T>(Stream stream, T obj, CancellationToken ct) where T : class;
}
```

Each serializer handles its own format details while providing a consistent API.

## Cross-Cutting Concerns

### Async/Await First

All I/O operations in Femur are fully asynchronous:
- **File operations**: `IFileSystem.ReadFileAsync()`, `WriteFileAsync()`
- **Serialization**: `IAsyncSerializer.SerializeAsync()`, `DeserializeAsync()`
- **HTTP**: HttpClient integration in examples
- **Host lifecycle**: `ApplicationBuilder.RunAsync()`, `IHostedService.StartAsync()`

**Why:** Async I/O scales better (threads aren't blocked waiting for I/O), reduces resource usage, and enables responsive applications.

### Dependency Injection

Femur is built on `Microsoft.Extensions.DependencyInjection`:
- All services register via `IServiceCollection`
- Lifetime management (singleton, scoped, transient) is consistent
- Constructor injection is the primary pattern
- Services can be tested by mocking interfaces

**Why:** DI promotes loose coupling, testability, and follows .NET ecosystem conventions.

### Interface-Based Design

Core abstractions are defined as interfaces:
- `IFileSystem`, `IAsyncSerializer`, `ILogger`, `IOptions<T>`
- Implementations can be swapped (testing, different environments)
- Enables decorator pattern (add caching, logging, metrics to any implementation)

**Why:** Interfaces are contracts that enable flexibility and testability.

### Multi-Targeting

Femur packages target multiple .NET versions:
- **netstandard2.0**: Maximum compatibility (works on .NET Framework 4.7.2+, .NET Core 2.0+, .NET 5+)
- **net8.0**: LTS version with performance optimizations
- **net9.0**: STS version with latest features
- **net10.0**: Cutting-edge support

**Why:** Libraries should work in diverse environments (legacy .NET Framework, modern .NET) while taking advantage of newer runtime improvements when available.

### Memory Efficiency

Femur prioritizes low memory usage:
- **ArrayPool<char>**: Parsers rent buffers from shared pool, reducing allocations
- **Streaming**: Parsers process files incrementally, not all-at-once
- **Struct where appropriate**: Value types for small, short-lived objects
- **StringBuilder reuse**: Single StringBuilder per parser, cleared between uses

**Why:** Memory efficiency improves scalability (more requests per server), reduces GC pressure, and enables resource-constrained deployments.

## Philosophy

Femur's design philosophy is captured in four principles:

1. **Convention over Configuration** - Common scenarios should work with minimal setup (`IStandardOptions` convention, default logging)
2. **Fail Fast** - Errors should be detected as early as possible (startup validation, compile-time checking where possible)
3. **Explicit over Implicit** - Magic behavior is avoided; configuration is clear and discoverable (fluent builders, explicit method calls)
4. **Performance by Default** - The default path should be fast (streaming parsers, async I/O, memory pooling)

These principles guide API design, feature implementation, and trade-off decisions.

## See Also

- [Getting Started](getting-started.md) - Build your first Femur application
- [API Data Aggregator Example](examples/api-aggregator/README.md) - See these concepts in action
- [Module Reference](README.md#module-reference) - Explore all packages
- [Logging Examples](../src/Logging/examples/AdvancedExample/README.md) - Advanced patterns with OpenTelemetry
