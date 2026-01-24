using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        var bootstrappedServices = new ServiceCollection();
        bootstrappedServices.AddLogging(b =>
        {
            b.ClearProviders();
            configure(b);
        });

        // Allow additional services to be registered (e.g., ActivitySource for OpenTelemetry)
        configureServices?.Invoke(bootstrappedServices);

        var serviceProvider = bootstrappedServices.BuildServiceProvider();

        return new BootstrapLogger<T>(bootstrappedServices, serviceProvider);
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
