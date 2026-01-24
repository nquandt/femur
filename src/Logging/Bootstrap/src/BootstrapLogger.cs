using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Femur.Hosting")]

namespace Femur.Logging.Bootstrap;

public class BootstrapLogger : ILogger, IDisposable, IAsyncDisposable
{
    private readonly ServiceCollection _bootstrappedServices;
    private readonly IServiceProvider _serviceProvider;

    private readonly Lazy<ILogger> _lazyLogger;
    private bool _disposed;

    internal BootstrapLogger(ServiceCollection bootstrappedServices, IServiceProvider serviceProvider, Lazy<ILogger>? lazyLogger = null)
    {
        this._bootstrappedServices = bootstrappedServices;
        this._serviceProvider = serviceProvider;
        this._lazyLogger = lazyLogger ?? new Lazy<ILogger>(() => serviceProvider.GetRequiredService<ILogger>());
    }

    public static BootstrapLogger Create<T>(Action<ILoggingBuilder> configure)
    {
        return Create<T>(configure, null);
    }

    public static BootstrapLogger Create<T>(Action<ILoggingBuilder> configure, Action<IServiceCollection>? configureServices)
    {
        return Create(typeof(T), configure, configureServices);
    }

    /// <summary>
    /// Creates a BootstrapLogger using a runtime Type parameter.
    /// Internal for use by hosting frameworks that perform type discovery.
    /// </summary>
    /// <param name="loggerType">The type to use for the ILogger category.</param>
    /// <param name="configure">An action to configure the logging builder.</param>
    /// <param name="configureServices">Optional action to configure additional services.</param>
    /// <returns>A configured BootstrapLogger instance.</returns>
    internal static BootstrapLogger Create(
        Type loggerType,
        Action<ILoggingBuilder> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var bootstrappedServices = new ServiceCollection();
        bootstrappedServices.AddLogging(b =>
        {
            b.ClearProviders();
            configure(b);
        });

        configureServices?.Invoke(bootstrappedServices);

        var serviceProvider = bootstrappedServices.BuildServiceProvider();
        var genericLoggerType = typeof(ILogger<>).MakeGenericType(loggerType);
        var lazyLogger = new Lazy<ILogger>(() => (ILogger)serviceProvider.GetRequiredService(genericLoggerType));

        return new BootstrapLogger(bootstrappedServices, serviceProvider, lazyLogger);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        this._lazyLogger.Value.Log(logLevel, eventId, state, exception, formatter);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return this._lazyLogger.Value.IsEnabled(logLevel);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return this._lazyLogger.Value.BeginScope(state);
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this._disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose the service provider, which will flush all logger providers
            if (this._serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        this._disposed = true;
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (this._disposed)
        {
            return;
        }

        if (disposing)
        {
            // Prefer async disposal if available to properly flush async logger providers
            if (this._serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (this._serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        this._disposed = true;
    }

    internal ServiceCollection BootstrappedServices => this._bootstrappedServices;

    internal IServiceProvider ServiceProvider => this._serviceProvider;
}

