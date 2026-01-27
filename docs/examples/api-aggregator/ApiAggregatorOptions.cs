using Femur;
using FluentValidation;

namespace ApiAggregator;

/// <summary>
/// Configuration options for the API aggregator with validation.
/// Demonstrates IStandardOptions pattern for convention-based configuration.
/// </summary>
public class ApiAggregatorOptions : IStandardOptions<ApiAggregatorOptions>
{
    /// <summary>
    /// Configuration section name that matches appsettings.json structure.
    /// </summary>
    public static string SectionName => "ApiAggregator";

    /// <summary>
    /// List of API endpoints to fetch data from.
    /// </summary>
    public List<ApiEndpoint> Endpoints { get; set; } = new();

    /// <summary>
    /// Output file path for aggregated JSON results.
    /// </summary>
    public string OutputFile { get; set; } = "output.json";

    /// <summary>
    /// Timeout in seconds for HTTP requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of concurrent HTTP requests.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// FluentValidation rules applied at application startup.
    /// If validation fails, the application exits before runtime with detailed error messages.
    /// </summary>
    public static void SetupValidator(AbstractValidator<ApiAggregatorOptions> validator)
    {
        // Endpoints list must not be empty
        validator.RuleFor(x => x.Endpoints)
            .NotEmpty()
            .WithMessage("At least one API endpoint must be configured");

        // Each endpoint must have a valid URL
        validator.RuleForEach(x => x.Endpoints)
            .Must(e => Uri.IsWellFormedUriString(e.Url, UriKind.Absolute))
            .WithMessage(e => $"Endpoint '{e.Name}' has invalid URL: {e.Url}");

        // Each endpoint must have a name
        validator.RuleForEach(x => x.Endpoints)
            .Must(e => !string.IsNullOrWhiteSpace(e.Name))
            .WithMessage("Each endpoint must have a name");

        // Output file must not be empty
        validator.RuleFor(x => x.OutputFile)
            .NotEmpty()
            .WithMessage("Output file path must be specified");

        // Timeout must be positive
        validator.RuleFor(x => x.TimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("Timeout must be greater than 0 seconds");

        // Max concurrent requests must be reasonable
        validator.RuleFor(x => x.MaxConcurrentRequests)
            .GreaterThan(0)
            .LessThanOrEqualTo(20)
            .WithMessage("MaxConcurrentRequests must be between 1 and 20");
    }
}
