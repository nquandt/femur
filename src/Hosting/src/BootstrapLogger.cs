using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Femur.Hosting;

public class BootstrapLogger : ILogger, IDisposable
{
    private ILogger Logger => this.GetLogger();
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;
    public BootstrapLogger(Action<ILoggingBuilder> configure)
    {
        // service collection
        var services = new ServiceCollection();
        _ = services.AddLogging(configure);
        this._serviceProvider = services.BuildServiceProvider();
    }

    private static readonly Lazy<Type> ProgramType = new(DiscoverProgramType);
    private static readonly Lazy<string> LoggerCategoryName = new(() => GetLoggerCategoryName(ProgramType.Value));

    /// <summary>
    /// Discovers the Program class type using reflection by looking for the entry point.
    /// </summary>
    /// <returns>The Type of the Program class, or a fallback type if not found.</returns>
    private static Type DiscoverProgramType()
    {
        try
        {
            // First, try to get the entry assembly
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                // Look for a class named "Program" first
                var programType = entryAssembly.GetType("Program") ??
                                entryAssembly.GetTypes().FirstOrDefault(t => t.Name == "Program");

                if (programType != null)
                {
                    return programType;
                }

                // If no Program class found, try to find the entry point method
                var entryPoint = entryAssembly.EntryPoint;
                if (entryPoint?.DeclaringType != null)
                {
                    return entryPoint.DeclaringType;
                }
            }

            // Fallback: try to find Program class in calling assembly
            var callingAssembly = Assembly.GetCallingAssembly();
            if (callingAssembly != null)
            {
                var programType = callingAssembly.GetType("Program") ??
                                callingAssembly.GetTypes().FirstOrDefault(t => t.Name == "Program");

                if (programType != null)
                {
                    return programType;
                }
            }

            // Final fallback: use the stack trace to find the calling type
            var stackTrace = new StackTrace();
            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame?.GetMethod();
                if (method?.DeclaringType != null
                    && method.DeclaringType.Name == "Program"
                    && method.Name == "Main")
                {
                    return method.DeclaringType;
                }
            }
        }
        catch
        {
            // If discovery fails, fall back to a generic type
        }

        // Ultimate fallback
        return typeof(object);
    }

    /// <summary>
    /// Gets an appropriate logger category name from the discovered type.
    /// </summary>
    /// <param name="programType">The discovered program type.</param>
    /// <returns>A logger category name derived from the type's namespace or assembly.</returns>
    private static string GetLoggerCategoryName(Type programType)
    {
        // If we have a namespace, use it (e.g., "MyApp.Program" -> "MyApp")
        if (!string.IsNullOrEmpty(programType.Namespace))
        {
            // If the namespace ends with the type name, use the namespace minus the type name
            var namespaceParts = programType.Namespace.Split('.');
            if (namespaceParts.Length > 1 && namespaceParts.Last() == programType.Name)
            {
                return string.Join(".", namespaceParts.Take(namespaceParts.Length - 1));
            }

            return programType.Namespace;
        }

        // Fallback to assembly name without extensions
        var assemblyName = programType.Assembly.GetName().Name;
        return assemblyName ?? "Application";
    }

    /// <summary>
    /// Gets the logger category name for the discovered program type. 
    /// Internal for use by Web extensions.
    /// </summary>
    /// <returns>A logger category name derived from the discovered program type.</returns>
    internal static string GetLoggerCategoryName()
    {
        return LoggerCategoryName.Value;
    }


    public ILogger GetLogger()
    {
        this.ThrowIfDisposed();
        var loggerType = typeof(ILogger<>).MakeGenericType(ProgramType.Value);
        var logger = (ILogger)this._serviceProvider.GetRequiredService(loggerType);

        return logger;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this._disposed)
        {
            return;
        }

        if (disposing)
        {
            if (this._serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        this._disposed = true;
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(BootstrapLogger));
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        this.Logger.Log(logLevel, eventId, state, exception, formatter);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return this.Logger.IsEnabled(logLevel);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return this.Logger.BeginScope(state);
    }
}