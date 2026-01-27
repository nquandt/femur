using Microsoft.Extensions.Logging;

namespace Femur.DependencyInjection;

/// <summary>
/// Proxied ILogger&lt;T&gt; that resolves from the source provider's ILoggerFactory.
/// </summary>
public sealed class ProxiedLogger<T> : ILogger<T>
{
    private readonly ILogger _inner;

    public ProxiedLogger(SourceProviderAccessor accessor)
    {
        var factory = accessor.GetRequiredService<ILoggerFactory>();
        this._inner = factory.CreateLogger<T>();
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => this._inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => this._inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => this._inner.Log(logLevel, eventId, state, exception, formatter);
}
