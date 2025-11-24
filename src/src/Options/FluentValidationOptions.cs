using FluentValidation;
using Microsoft.Extensions.Options;

namespace Femur.Options;

public class FluentValidationOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IValidator<TOptions>? _validator;

    public FluentValidationOptions(IValidator<TOptions>? validator)
    {
        this._validator = validator;
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (this._validator is null)
        {
            return ValidateOptionsResult.Fail($"No validator registered for {typeof(TOptions).Name}");
        }

        if (options is null)
        {
            return ValidateOptionsResult.Fail($"Provided options are null for {typeof(TOptions).Name}");
        }

        var validationResult = this._validator.Validate(options);

        if (validationResult.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = string.Join(", ", validationResult.Errors);

        return ValidateOptionsResult.Fail($"FluentValidation failed for {typeof(TOptions).Name}: {errors}");
    }
}
