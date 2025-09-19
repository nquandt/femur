using Microsoft.AspNetCore.Builder;

namespace Femur.Hosting.Web;

/// <summary>
/// Provides extension methods for FemurApplicationBuilder to support ASP.NET Core Web applications.
/// </summary>
public static class ApplicationBuilderWebExtensions
{
    /// <summary>
    /// Configures the application pipeline using an asynchronous configuration function.
    /// This extension converts the ApplicationBuilder from Console mode to Web mode.
    /// Must be called after services are configured.
    /// </summary>
    /// <param name="builder">The ApplicationBuilder instance after services configuration.</param>
    /// <param name="configure">An asynchronous function to configure the WebApplication pipeline.</param>
    /// <returns>A WebApplicationBuilder instance for method chaining.</returns>
    public static WebApplicationBuilder ConfigureAsWebApplication(
        this IServicesApplicationBuilder builder,
        Func<WebApplication, Task> configure)
    {
        // Cast back to the concrete type to access internal properties
        var concreteBuilder = (ApplicationBuilder)builder;
        return new WebApplicationBuilder(concreteBuilder, configure);
    }

    /// <summary>
    /// Configures the application pipeline using a synchronous configuration action.
    /// This extension converts the ApplicationBuilder from Console mode to Web mode.
    /// Must be called after services are configured.
    /// </summary>
    /// <param name="builder">The ApplicationBuilder instance after services configuration.</param>
    /// <param name="configure">An action to configure the WebApplication pipeline.</param>
    /// <returns>A WebApplicationBuilder instance for method chaining.</returns>
    public static WebApplicationBuilder ConfigureAsWebApplication(
        this IServicesApplicationBuilder builder,
        Action<WebApplication> configure)
    {
        // Cast back to the concrete type to access internal properties
        var concreteBuilder = (ApplicationBuilder)builder;
        return new WebApplicationBuilder(concreteBuilder, app =>
        {
            configure(app);
            return Task.CompletedTask;
        });
    }
}