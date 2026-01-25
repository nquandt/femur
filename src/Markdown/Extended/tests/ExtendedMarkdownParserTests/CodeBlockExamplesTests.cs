using Femur.Markdown.Extended.Parser;
using Femur.Markdown.Extended.Abstractions.Nodes;
using Femur.Markdown.Abstractions.Nodes;
using Xunit;

namespace ExtendedMarkdownParserTests;

/// <summary>
/// Tests for parsing markdown documents with extended code block examples.
/// Based on real-world examples from documentation articles.
/// </summary>
public class CodeBlockExamplesTests
{
    [Fact]
    public void Parse_DocumentWithFrontmatterAndCodeBlocks_ParsesCorrectly()
    {
        // Arrange - Example from IOptions FluentValidation article
        var markdown = """
---
title: "IOptions FluentValidation at Startup"
slug: "ioptions-fluentvalidation"
lang: en
description: "Move configuration validation to application startup using FluentValidation."
---

# Introduction

Some content here.

:::C:Codeblock {lang="csharp"}
    public class EmailOptions
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string ApiKey { get; set; }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("IOptions FluentValidation at Startup", document.FrontMatterBlock!.ParsedData!["title"]);
        Assert.Equal("ioptions-fluentvalidation", document.FrontMatterBlock!.ParsedData!["slug"]);
        Assert.Equal("en", document.FrontMatterBlock!.ParsedData!["lang"]);
        
        // Check for heading
        var heading = document.Children.OfType<HeadingNode>().FirstOrDefault();
        Assert.NotNull(heading);
        Assert.Equal(1, heading.Level);
        
        // Check for fenced div code block
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        Assert.Equal("C:Codeblock", fencedDiv.Tag);
        Assert.Equal("csharp", fencedDiv.ParsedAttributes.KeyValueAttributes["lang"]);

        // C:Codeblock tags use colon convention - they don't parse children, only store rawContent
        Assert.False(fencedDiv.HasChildren);
        Assert.NotNull(fencedDiv.RawContent);
        Assert.Contains("EmailOptions", fencedDiv.RawContent);
        Assert.Contains("SmtpServer", fencedDiv.RawContent);
        Assert.Contains("public class EmailOptions", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_MultipleCodeBlocksInSequence_ParsesAll()
    {
        // Arrange
        var markdown = """
---
title: Test
---

:::C:Codeblock {lang="csharp"}
    public class EmailOptions
    {
        public string SmtpServer { get; set; }
    }
:::

:::C:Codeblock {lang="csharp"}
    public class EmailService
    {
        private readonly EmailOptions _options;
        
        public EmailService(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDivs = document.Children.OfType<FencedDivNode>().ToList();
        Assert.Equal(2, fencedDivs.Count);

        Assert.Equal("C:Codeblock", fencedDivs[0].Tag);
        Assert.Contains("EmailOptions", fencedDivs[0].RawContent);

        Assert.Equal("C:Codeblock", fencedDivs[1].Tag);
        Assert.Contains("EmailService", fencedDivs[1].RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithInterfaceDefinitions_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Interfaces
---

:::C:Codeblock {lang="csharp"}
    public interface IStandardOptions
    {
        static abstract string SectionName { get; }
    }

    public interface IStandardOptionsWithValidation<TOptions> : IStandardOptions
        where TOptions : class, IStandardOptionsWithValidation<TOptions>
    {
        static abstract void SetupValidator(AbstractValidator<TOptions> validator);
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("IStandardOptions", fencedDiv.RawContent);
        Assert.Contains("IStandardOptionsWithValidation", fencedDiv.RawContent);
        Assert.Contains("static abstract", fencedDiv.RawContent);
        Assert.Contains("where TOptions", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithClassAndValidation_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Configuration Class
---

:::C:Codeblock {lang="csharp"}
    public class EmailOptions : IStandardOptionsWithValidation<EmailOptions>
    {
        public static string SectionName => "Email";
        
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        
        public static void SetupValidator(AbstractValidator<EmailOptions> validator)
        {
            validator.RuleFor(x => x.SmtpServer)
                .NotEmpty()
                .Must(x => Uri.CheckHostName(x) != UriHostNameType.Unknown)
                .WithMessage("SmtpServer must be a valid hostname");
                
            validator.RuleFor(x => x.Port)
                .InclusiveBetween(1, 65535);
                
            validator.RuleFor(x => x.ApiKey)
                .NotEmpty()
                .MinimumLength(32);
        }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("EmailOptions", fencedDiv.RawContent);
        Assert.Contains("IStandardOptionsWithValidation", fencedDiv.RawContent);
        Assert.Contains("SetupValidator", fencedDiv.RawContent);
        Assert.Contains("RuleFor", fencedDiv.RawContent);
        Assert.Contains("NotEmpty", fencedDiv.RawContent);
        Assert.Contains("InclusiveBetween", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithServiceRegistrationExtensions_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Service Registration
---

:::C:Codeblock {lang="csharp"}
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddOptionsWithValidation<TOptions>(
            this IServiceCollection services)
            where TOptions : class, IStandardOptionsWithValidation<TOptions>
        {
            var sectionName = TOptions.SectionName;
            
            // Register FluentValidation validator
            services.TryAddSingleton<IValidator<TOptions>>(sp => 
                new OptionsValidator<TOptions>(TOptions.SetupValidator));
            
            // Configure standard options binding
            services.AddOptions<TOptions>()
                .BindConfiguration(sectionName)
                .ValidateFluentValidation()
                .ValidateOnStart();
            
            return services;
        }
        
        public static OptionsBuilder<TOptions> ValidateFluentValidation<TOptions>(
            this OptionsBuilder<TOptions> builder) 
            where TOptions : class
        {
            builder.Services.AddSingleton<IValidateOptions<TOptions>>(sp =>
                new FluentValidationOptions<TOptions>(
                    sp.GetService<IValidator<TOptions>>()));
            
            return builder;
        }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("ServiceCollectionExtensions", fencedDiv.RawContent);
        Assert.Contains("AddOptionsWithValidation", fencedDiv.RawContent);
        Assert.Contains("ValidateFluentValidation", fencedDiv.RawContent);
        Assert.Contains("TryAddSingleton", fencedDiv.RawContent);
        Assert.Contains("BindConfiguration", fencedDiv.RawContent);
        Assert.Contains("ValidateOnStart", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithFluentValidationIntegration_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: FluentValidation Integration
---

:::C:Codeblock {lang="csharp"}
    public class OptionsValidator<TOptions> : AbstractValidator<TOptions>
        where TOptions : class, IStandardOptionsWithValidation<TOptions>
    {
        public OptionsValidator(Action<AbstractValidator<TOptions>> configure)
        {
            configure(this);
        }
    }

    public class FluentValidationOptions<TOptions> : IValidateOptions<TOptions>
        where TOptions : class
    {
        private readonly IValidator<TOptions>? _validator;
        
        public FluentValidationOptions(IValidator<TOptions>? validator)
        {
            _validator = validator;
        }
        
        public ValidateOptionsResult Validate(string? name, TOptions options)
        {
            if (_validator is null)
            {
                return ValidateOptionsResult.Fail(
                    $"No validator registered for {typeof(TOptions).Name}");
            }
            
            ArgumentNullException.ThrowIfNull(options);
            
            var result = _validator.Validate(options);
            
            if (result.IsValid)
            {
                return ValidateOptionsResult.Success;
            }
            
            var errors = string.Join("; ", result.Errors.Select(e => 
                $"{e.PropertyName}: {e.ErrorMessage}"));
            
            return ValidateOptionsResult.Fail(
                $"Validation failed for {typeof(TOptions).Name}: {errors}");
        }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("OptionsValidator", fencedDiv.RawContent);
        Assert.Contains("FluentValidationOptions", fencedDiv.RawContent);
        Assert.Contains("AbstractValidator", fencedDiv.RawContent);
        Assert.Contains("IValidateOptions", fencedDiv.RawContent);
        Assert.Contains("Validate", fencedDiv.RawContent);
        Assert.Contains("ValidateOptionsResult", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithProgramCsUsage_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Usage Example
---

:::C:Codeblock {lang="csharp"}
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddOptionsWithValidation<EmailOptions>();

    var app = builder.Build();

    // Validation happens here during Host.StartAsync() when app.Run() is called
    app.Run();
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        Assert.False(fencedDiv.HasChildren);
        Assert.Contains("WebApplication.CreateBuilder", fencedDiv.RawContent);
        Assert.Contains("AddOptionsWithValidation", fencedDiv.RawContent);
        Assert.Contains("builder.Build", fencedDiv.RawContent);
        Assert.Contains("app.Run", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithEarlyValidationExample_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Early Validation
---

:::C:Codeblock {lang="csharp"}
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddOptionsWithValidation<EmailOptions>();

    var app = builder.Build();

    // Force immediate validation before starting the host
    var validator = app.Services.GetService<IStartupValidator>();
    if (validator is not null)
    {
        validator.Validate(); // Throws OptionsValidationException on failure
    }

    app.Run();
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("GetService", fencedDiv.RawContent);
        Assert.Contains("IStartupValidator", fencedDiv.RawContent);
        Assert.Contains("validator.Validate", fencedDiv.RawContent);
        Assert.Contains("OptionsValidationException", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithConditionalRegistration_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Conditional Registration
---

:::C:Codeblock {lang="csharp"}
    if (builder.Environment.IsProduction())
    {
        builder.Services.AddOptionsWithValidation<EmailOptions>();
    }
    else
    {
        builder.Services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName);
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("IsProduction", fencedDiv.RawContent);
        Assert.Contains("AddOptionsWithValidation", fencedDiv.RawContent);
        Assert.Contains("BindConfiguration", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithHealthCheckExample_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Health Checks
---

:::C:Codeblock {lang="csharp"}
    var builder = WebApplication.CreateBuilder(args);

    // Validate configuration structure
    builder.Services.AddOptionsWithValidation<EmailOptions>();
    builder.Services.AddOptionsWithValidation<DatabaseOptions>();

    // Check external dependencies
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database")
        .AddCheck<EmailServiceHealthCheck>("email")
        .AddNpgSql(builder.Configuration.GetConnectionString("Default")!)
        .AddUrlGroup(new Uri("https://api.external.com/health"), "external-api");

    var app = builder.Build();

    app.MapHealthChecks("/health");
    app.Run();
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("AddHealthChecks", fencedDiv.RawContent);
        Assert.Contains("AddCheck", fencedDiv.RawContent);
        Assert.Contains("AddNpgSql", fencedDiv.RawContent);
        Assert.Contains("AddUrlGroup", fencedDiv.RawContent);
        Assert.Contains("MapHealthChecks", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithHealthCheckImplementation_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Health Check Implementation
---

:::C:Codeblock {lang="csharp"}
    public class EmailServiceHealthCheck : IHealthCheck
    {
        private readonly IOptions<EmailOptions> _options;
        
        public EmailServiceHealthCheck(IOptions<EmailOptions> options)
        {
            _options = options;
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Actually test connectivity
                using var client = new SmtpClient(_options.Value.SmtpServer, _options.Value.Port);
                await client.ConnectAsync(cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);
                
                return HealthCheckResult.Healthy("Email service is reachable");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "Email service is unreachable", 
                    ex);
            }
        }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("EmailServiceHealthCheck", fencedDiv.RawContent);
        Assert.Contains("IHealthCheck", fencedDiv.RawContent);
        Assert.Contains("CheckHealthAsync", fencedDiv.RawContent);
        Assert.Contains("SmtpClient", fencedDiv.RawContent);
        Assert.Contains("HealthCheckResult.Healthy", fencedDiv.RawContent);
        Assert.Contains("HealthCheckResult.Unhealthy", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_DocumentWithMixedContent_ParsesCorrectly()
    {
        // Arrange - Complex document with frontmatter, headings, paragraphs, and code blocks
        var markdown = """
---
title: "IOptions FluentValidation at Startup"
slug: "ioptions-fluentvalidation"
lang: en
description: "Move configuration validation to application startup using FluentValidation."
---

Configuration errors discovered at runtime—after your application has already started serving traffic—are among the most frustrating failures to debug.

## The Problem: Delayed Failure Discovery

Consider a typical scenario:

:::C:Codeblock {lang="csharp"}
    public class EmailOptions
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string ApiKey { get; set; }
    }
:::

If `EmailService` is only used by a specific endpoint that sends notifications, your application might run for hours before anyone notices the configuration is broken.

## The Solution: Validate on Startup

By moving validation to application startup, we can fail fast—the application refuses to start if configuration is invalid.

### Core Interfaces

Define standard interfaces for your configuration classes:

:::C:Codeblock {lang="csharp"}
    public interface IStandardOptions
    {
        static abstract string SectionName { get; }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("IOptions FluentValidation at Startup", document.FrontMatterBlock!.ParsedData!["title"]);
        
        // Check headings
        var headings = document.Children.OfType<HeadingNode>().ToList();
        Assert.True(headings.Count >= 2);
        Assert.Equal(2, headings[0].Level);
        Assert.Contains("Problem", headings[0].Children.OfType<MarkdownTextNode>().First().Content);
        
        // Check paragraphs
        var paragraphs = document.Children.OfType<ParagraphNode>().ToList();
        Assert.True(paragraphs.Count >= 1);
        
        // Check fenced divs
        var fencedDivs = document.Children.OfType<FencedDivNode>().ToList();
        Assert.True(fencedDivs.Count >= 2);
        
        // First code block
        var firstDiv = fencedDivs[0];
        Assert.Equal("C:Codeblock", firstDiv.Tag);
        Assert.False(firstDiv.HasChildren);
        Assert.Contains("EmailOptions", firstDiv.RawContent);

        // Second code block
        var secondDiv = fencedDivs[1];
        Assert.Equal("C:Codeblock", secondDiv.Tag);
        Assert.False(secondDiv.HasChildren);
        Assert.Contains("IStandardOptions", secondDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithIndentedCodeInside_StoresInRawContent()
    {
        // Arrange - C:Codeblock uses colon convention, so inner content is not parsed
        var markdown = """
---
title: Indented Code
---

:::C:Codeblock {lang="csharp"}
    public class TestClass
    {
        public void Method() { }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren); // Colon convention: no parsing

        Assert.Contains("TestClass", fencedDiv.RawContent);
        Assert.Contains("Method", fencedDiv.RawContent);
        // RawContent preserves indentation
        Assert.Contains("    public class TestClass", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithFencedCodeInside_StoresInRawContent()
    {
        // Arrange - C:Codeblock uses colon convention, so inner content is not parsed
        var markdown = """
---
title: Nested Fenced Code
---

:::C:Codeblock {lang="csharp"}
```csharp
public class Nested
{
    public string Property { get; set; }
}
```
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren); // Colon convention: no parsing

        // RawContent should contain the fenced code syntax
        Assert.Contains("```csharp", fencedDiv.RawContent);
        Assert.Contains("Nested", fencedDiv.RawContent);
        Assert.Contains("Property", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_MultipleCodeBlocksWithTextBetween_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Multiple Blocks
---

First paragraph explaining the concept.

:::C:Codeblock {lang="csharp"}
    public class First { }
:::

Second paragraph with more explanation.

:::C:Codeblock {lang="csharp"}
    public class Second { }
:::

Final paragraph.
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var paragraphs = document.Children.OfType<ParagraphNode>().ToList();
        Assert.True(paragraphs.Count >= 3);
        
        var fencedDivs = document.Children.OfType<FencedDivNode>().ToList();
        Assert.Equal(2, fencedDivs.Count);

        Assert.False(fencedDivs[0].HasChildren);
        Assert.Contains("First", fencedDivs[0].RawContent);

        Assert.False(fencedDivs[1].HasChildren);
        Assert.Contains("Second", fencedDivs[1].RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithComplexCSharpCode_PreservesAllContent()
    {
        // Arrange - Complex C# code with generics, async, LINQ, etc.
        var markdown = """
---
title: Complex Code
---

:::C:Codeblock {lang="csharp"}
    public async Task<ValidateOptionsResult> ValidateAsync<TOptions>(
        string? name, 
        TOptions options,
        CancellationToken cancellationToken = default)
        where TOptions : class
    {
        var errors = result.Errors
            .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
            .ToList();
        
        return errors.Count > 0
            ? ValidateOptionsResult.Fail(string.Join("; ", errors))
            : ValidateOptionsResult.Success;
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().First();
        Assert.False(fencedDiv.HasChildren);

        Assert.Contains("ValidateAsync", fencedDiv.RawContent);
        Assert.Contains("where TOptions", fencedDiv.RawContent);
        Assert.Contains("Select", fencedDiv.RawContent);
        Assert.Contains("ToList", fencedDiv.RawContent);
        Assert.Contains("ValidateOptionsResult", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_CodeBlockWithTwoClassesInSingleBlock_ParsesBothClasses()
    {
        // Arrange - Test that a single code block can contain multiple classes
        var markdown = """
---
title: Multiple Classes Example
---

:::C:Codeblock {lang="csharp"}
    public class EmailOptions
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; }
        public string ApiKey { get; set; } = string.Empty;
    }

    public class EmailService
    {
        private readonly EmailOptions _options;
        
        public EmailService(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }
        
        public void SendEmail(string to, string subject, string body)
        {
            // Implementation here
        }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        Assert.Equal("C:Codeblock", fencedDiv.Tag);
        Assert.False(fencedDiv.HasChildren);

        // Verify both classes are present in the raw content
        Assert.Contains("public class EmailOptions", fencedDiv.RawContent);
        Assert.Contains("public class EmailService", fencedDiv.RawContent);
        Assert.Contains("SmtpServer", fencedDiv.RawContent);
        Assert.Contains("SendEmail", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_FencedDivWithIndentedCode_RawContentPreservesIndentation()
    {
        // Arrange
        var markdown = """
---
title: Raw Content Test
---

:::C:Codeblock {lang="csharp"}
    public class TestClass
    {
        public void Method() { }
    }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        Assert.False(fencedDiv.HasChildren); // Colon convention: no parsing

        // RawContent should preserve the original indentation
        Assert.NotNull(fencedDiv.RawContent);
        Assert.Contains("    public class TestClass", fencedDiv.RawContent);
        Assert.Contains("        public void Method()", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_FencedDivWithFencedCodeBlock_RawContentContainsFencedSyntax()
    {
        // Arrange
        var markdown = """
---
title: Fenced Code Test
---

:::C:Codeblock {lang="csharp"}
```csharp
public class Nested
{
    public string Property { get; set; }
}
```
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        Assert.False(fencedDiv.HasChildren); // Colon convention: no parsing

        // RawContent should contain the fenced code block syntax
        Assert.NotNull(fencedDiv.RawContent);
        Assert.Contains("```csharp", fencedDiv.RawContent);
        Assert.Contains("```", fencedDiv.RawContent);
        Assert.Contains("public class Nested", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_FencedDivWithMixedContent_RawContentContainsAllContent()
    {
        // Arrange
        var markdown = """
---
title: Mixed Content Test
---

:::C:Codeblock {lang="csharp"}
    public class First { }

Some text between code blocks.

    public class Second { }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        Assert.False(fencedDiv.HasChildren); // Colon convention: no parsing

        // RawContent should contain all the raw markdown content
        Assert.NotNull(fencedDiv.RawContent);
        Assert.Contains("public class First", fencedDiv.RawContent);
        Assert.Contains("Some text between code blocks", fencedDiv.RawContent);
        Assert.Contains("public class Second", fencedDiv.RawContent);
    }

    [Fact]
    public void Parse_FencedDivRawContent_MatchesOriginalContent()
    {
        // Arrange
        var expectedContent = "    public class EmailOptions\n    {\n        public string SmtpServer { get; set; }\n        public int Port { get; set; }\n    }";
        
        var markdown = $$"""
---
title: Raw Content Match Test
---

:::C:Codeblock {lang="csharp"}
{{expectedContent}}
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        
        // RawContent should match the original content exactly (with newlines normalized)
        Assert.NotNull(fencedDiv.RawContent);
        var normalizedRaw = fencedDiv.RawContent.Replace("\r\n", "\n").TrimEnd();
        var normalizedExpected = expectedContent.Replace("\r\n", "\n");
        Assert.Equal(normalizedExpected, normalizedRaw);
    }

    [Fact]
    public void Parse_FencedDivWithEmptyContent_RawContentIsEmpty()
    {
        // Arrange
        var markdown = """
---
title: Empty Content Test
---

:::C:Codeblock {lang="csharp"}
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDiv = document.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(fencedDiv);
        
        // RawContent should be empty or whitespace only
        Assert.NotNull(fencedDiv.RawContent);
        Assert.True(string.IsNullOrWhiteSpace(fencedDiv.RawContent));
    }

    [Fact]
    public void Parse_MultipleFencedDivs_EachHasCorrectRawContent()
    {
        // Arrange
        var markdown = """
---
title: Multiple Divs Test
---

:::C:Codeblock {lang="csharp"}
    public class First { }
:::

:::C:Codeblock {lang="csharp"}
    public class Second { }
:::
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        var fencedDivs = document.Children.OfType<FencedDivNode>().ToList();
        Assert.Equal(2, fencedDivs.Count);
        
        // Each div should have its own RawContent
        Assert.NotNull(fencedDivs[0].RawContent);
        Assert.Contains("First", fencedDivs[0].RawContent);
        Assert.DoesNotContain("Second", fencedDivs[0].RawContent);
        
        Assert.NotNull(fencedDivs[1].RawContent);
        Assert.Contains("Second", fencedDivs[1].RawContent);
        Assert.DoesNotContain("First", fencedDivs[1].RawContent);
        
        // RawContent should be different for each div
        Assert.NotEqual(fencedDivs[0].RawContent, fencedDivs[1].RawContent);
    }
}
