using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Femur.Tests;

public class ServiceCollectionTests
{
    private readonly ITestOutputHelper _output;

    public ServiceCollectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TryConfigureByConventionWithValidation_ShouldNotThrow_WhenOptionsAreGood()
    {
        //Arrange
        var configurationBuilder = new ConfigurationBuilder();

        var json = "{\"TestSection\": { \"BaseUrl\": \"https://www.github.com\" } }";
        var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        configurationBuilder.AddJsonStream(jsonStream);

        var configuration = configurationBuilder.Build();
        var services = new ServiceCollection();
        services.TryAddSingleton<IConfiguration>(configuration);

        services.TryConfigureByConventionWithValidation<TestSectionOptions>();
        var sp = services.BuildServiceProvider();


        //Act

        var validator = sp.GetRequiredService<IStartupValidator>();
        var task = Task.Run(() =>
        {
            validator.Validate();
        });

        await task;

        var options = sp.GetRequiredService<IOptions<TestSectionOptions>>();

        //Assert

        Assert.Equal("Microsoft.Extensions.Options.StartupValidator", validator.GetType().FullName);

        Assert.Equal("https://www.github.com", options.Value.BaseUrl);
        Assert.True(task.IsCompletedSuccessfully); //Validation Success  
    }

    [Fact]
    public async Task TryConfigureByConventionWithValidation_ShouldThrow_WhenOptionsBad()
    {
        //Arrange
        var configurationBuilder = new ConfigurationBuilder();

        var json = "{\"TestSection\": { \"BaseUrl\": \"hsl/llama\" } }";
        var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        configurationBuilder.AddJsonStream(jsonStream);

        var configuration = configurationBuilder.Build();
        var services = new ServiceCollection();
        services.TryAddSingleton<IConfiguration>(configuration);

        services.TryConfigureByConventionWithValidation<TestSectionOptions>();
        var sp = services.BuildServiceProvider();


        //Act
        var validator = sp.GetRequiredService<IStartupValidator>();
        var task = () => Task.Run(() =>
        {
            validator.Validate();
        });



        //Assert

        Assert.Equal("Microsoft.Extensions.Options.StartupValidator", validator.GetType().FullName);

        var except = await Assert.ThrowsAsync<Microsoft.Extensions.Options.OptionsValidationException>(task);

        // _output.WriteLine(except.Message);
        Assert.Contains("FluentValidation failed for", except.Message);
        Assert.Contains("must not be a valid Uri", except.Message);
    }

    [Fact]
    public async Task TryConfigureByConventionWithValidation_ShouldThrow_WhenOptionsMissing()
    {
        //Arrange
        var configurationBuilder = new ConfigurationBuilder();

        var configuration = configurationBuilder.Build();
        var services = new ServiceCollection();
        services.TryAddSingleton<IConfiguration>(configuration);

        services.TryConfigureByConventionWithValidation<TestSectionOptions>();
        var sp = services.BuildServiceProvider();

        //Act
        var validator = sp.GetRequiredService<IStartupValidator>();
        var task = () => Task.Run(() =>
        {
            validator.Validate();
        });


        //Assert
        Assert.Equal("Microsoft.Extensions.Options.StartupValidator", validator.GetType().FullName);
        var except = await Assert.ThrowsAsync<Microsoft.Extensions.Options.OptionsValidationException>(task);

        // _output.WriteLine(except.Message);
        Assert.Contains("FluentValidation failed for", except.Message);
        Assert.Contains("must not be a valid Uri", except.Message);
    }
}

public class TestService
{
    public required string Property { get; set; }
}

public class TestSectionOptions : IStandardOptions<TestSectionOptions>
{
    public static string SectionName => "TestSection";

    public static void SetupValidator(AbstractValidator<TestSectionOptions> validator)
    {
        validator.RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .WithMessage($"{nameof(TestSectionOptions.BaseUrl)} must not be empty")
            .Must(x => x?.StartsWith("/") == true ? Uri.TryCreate(x, UriKind.Relative, out var _) : Uri.TryCreate(x, UriKind.Absolute, out var _))
            .WithMessage($"{nameof(TestSectionOptions.BaseUrl)} must not be a valid Uri");
    }

    public required string BaseUrl { get; set; }
}


[Obsolete]
public class TestOptionsValidator : AbstractValidator<TestSectionOptions>
{
    public TestOptionsValidator()
    {
        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .WithMessage($"{nameof(TestSectionOptions.BaseUrl)} must not be empty")
            .Must(x => x?.StartsWith("/") == true ? Uri.TryCreate(x, UriKind.Relative, out var _) : Uri.TryCreate(x, UriKind.Absolute, out var _))
            .WithMessage($"{nameof(TestSectionOptions.BaseUrl)} must not be a valid Uri");
    }
}

