using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Femur.Options;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection TryConfigureByConventionWithValidation<TOptions>(this IServiceCollection services)
        where TOptions : class, IStandardOptions<TOptions>
    {
        var name = string.Empty;

        var sectionName = TOptions.SectionName;

        services.TryAddSingleton<IValidator<TOptions>>(sp => new DefaultValidator<TOptions>(TOptions.SetupValidator));

        _ = services.AddOptions<TOptions>();

        _ = services.AddValidationFor<IOptionsMonitor<TOptions>>((opt, sp) =>
        {
            var fl = new FluentValidationOptions<TOptions>(sp.GetService<IValidator<TOptions>>());

            return fl.Validate(null, opt.CurrentValue);
        });

        _ = services.TryConfigureByConvention<TOptions>();

        return services;
    }

    internal static IServiceCollection TryConfigureByConvention<TOptions>(this IServiceCollection services)
        where TOptions : class, IStandardOptions<TOptions>
    {
        var name = string.Empty;

        var sectionName = TOptions.SectionName;

        services.TryAddSingleton<IOptionsChangeTokenSource<TOptions>>(sp => new ConfigurationChangeTokenSource<TOptions>(name, sp.GetRequiredService<IConfiguration>().GetSection(sectionName)));
        services.TryAddSingleton<IConfigureOptions<TOptions>>(sp => new NamedConfigureFromConfigurationOptions<TOptions>(name, sp.GetRequiredService<IConfiguration>().GetSection(sectionName), _ => { }));

        return services;
    }

    internal static IServiceCollection AddValidationFor<TDep>(this IServiceCollection services, Func<TDep, IServiceProvider, ValidateOptionsResult> func)
        where TDep : class
    {
        _ = services.AddOptions<FakeOptions<TDep>>()
            .Configure(x => { })
            .ValidateOnStart();

        _ = services.AddSingleton<IValidateOptions<FakeOptions<TDep>>>(sp => new BooleanServiceValidator<TDep>(func, sp));

        return services;
    }
}

internal sealed class BooleanServiceValidator<TOptions> : IValidateOptions<FakeOptions<TOptions>>
    where TOptions : class
{
    private readonly Func<TOptions, IServiceProvider, ValidateOptionsResult> _validationAction;
    private readonly IServiceProvider _serviceProvider;

    public BooleanServiceValidator(Func<TOptions, IServiceProvider, ValidateOptionsResult> func, IServiceProvider serviceProvider)
    {
        this._validationAction = func;
        this._serviceProvider = serviceProvider;
    }

    public ValidateOptionsResult Validate(string? name, FakeOptions<TOptions> options)
    {
        var actualService = this._serviceProvider.GetService<TOptions>();

        if (actualService is null)
        {
            return ValidateOptionsResult.Fail($"Service of type {typeof(TOptions).Name} was not found in DI container");
        }

        return this._validationAction(actualService, this._serviceProvider);
    }
}
