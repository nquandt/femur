# Femur

> A strong foundation for scalable .NET applications.


Femur is a .NET utilities library designed to simplify application development at scale. Leaning into proven software design principles, it aims to provide well-structured, reusable tooling that promotes maintainability, flexibility, and efficiency.

The focus is on creating a strong foundation that empowers developers to build scalable applications with confidence, reducing complexity while ensuring long-term adaptability.

## Libraries

### Core
- **Femur** - Core utilities and foundational tooling

### ASP.NET Core
- **Femur.AspNetCore** - ASP.NET Core utilities and extensions
- **Femur.AspNetCore.Endpoints** - Delegate-based minimal API endpoints with DI support

### Dependency Injection
- **Femur.DependencyInjection** - Proxy services across ServiceProvider boundaries, preserving lifetimes and handling open generics

### File System
- **Femur.FileSystem** - File system abstractions with default, in-memory, and Azure implementations
- **Femur.FileSystem.AzureBlob** - Azure Blob Storage file system provider

### Hosting
- **Femur.Hosting** - Application hosting utilities and patterns
- **Femur.Hosting.Web** - Web application hosting extensions

### Logging
- **Femur.Logging.Bootstrap** - Early logging during application startup, before the host is fully initialized

### Markdown
- **Femur.Markdown.Abstractions** - Abstract Syntax Tree (AST) node types and utilities for Markdown documents
- **Femur.Markdown.Parser** - Streaming CommonMark 0.31.2 parser
- **Femur.Markdown.Renderer** - HTML renderer for Markdown ASTs
- **Femur.Markdown.Extended.Abstractions** - Extended abstractions with YAML frontmatter support
- **Femur.Markdown.Extended.Parser** - Extended Markdown parser with YAML frontmatter

### Messaging
- **Femur.Messaging** - Simple, opinionated message processing framework with clean abstractions
- **Femur.Messaging.InMemory** - In-memory transport for testing and local development
- **Femur.Messaging.ServiceBus** - Azure Service Bus transport implementation

### Parsing
- **Femur.Parsing** - Core abstractions and base classes for streaming parsers with buffer management
- **Femur.Markup.Abstractions** - Shared abstractions for markup-based parsers (HTML, XML)
- **Femur.Html.Parser** - Streaming HTML 2.0 parser
- **Femur.Xml.Abstractions** - XML-specific abstractions and node types
- **Femur.Xml.Parser** - Streaming XML parser with strict validation

### Serialization
- **Femur.Serialization** - Flexible, extensible async serialization framework with JSON support