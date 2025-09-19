using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Femur.Hosting;

interface IConsoleApplicationHostedService
{
    int ExitCode { get; }
}


/// <summary>
/// A hosted service wrapper that executes an IConsoleApplication and manages the application lifetime.
/// </summary>
/// <typeparam name="TApplication">The IConsoleApplication implementation to execute.</typeparam>
internal class ConsoleApplicationHostedService<TApplication> : BackgroundService, IConsoleApplicationHostedService
    where TApplication : class, IConsoleApplication
{
    private readonly TApplication _application;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ConsoleApplicationHostedService<TApplication>> _logger;
    private int _exitCode = -1;

    public ConsoleApplicationHostedService(
        TApplication application,
        IHostApplicationLifetime lifetime,
        ILogger<ConsoleApplicationHostedService<TApplication>> logger)
    {
        this._application = application;
        this._lifetime = lifetime;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the exit code returned by the console application.
    /// </summary>
    public int ExitCode => this._exitCode;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            this._logger.LogInformation("Starting console application execution");
            this._exitCode = await this._application.ExecuteAsync(stoppingToken);
            this._logger.LogInformation("Console application execution completed with exit code {exitCode}", this._exitCode);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application was cancelled via the stopping token (Ctrl+C, shutdown, etc.)
            // Check if the application had already set an exit code before the cancellation
            if (this._exitCode == ExitCodes.Success)
            {
                this._exitCode = ExitCodes.CtrlCInterrupt; // Standard POSIX exit code for SIGINT (Ctrl+C)
            }

            this._logger.LogWarning(ExitCodes.Messages.CtrlCInterrupt, this._exitCode);
            // Don't re-throw cancellation exceptions as they're expected during shutdown
        }
        catch (OperationCanceledException)
        {
            // Application was cancelled by some other cancellation token
            if (this._exitCode == ExitCodes.Success)
            {
                this._exitCode = ExitCodes.CommandCancelled; // Command cancelled
            }

            this._logger.LogWarning(ExitCodes.Messages.CommandCancelled, this._exitCode);
            // Don't re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            this._exitCode = ExitCodes.RuntimeError; // General runtime error exit code
            this._logger.LogError(ex, ExitCodes.Messages.ApplicationExecutionError, this._exitCode);
            throw; // Re-throw to ensure proper error handling by the hosting framework
        }
        finally
        {
            // Stop the application gracefully
            this._lifetime.StopApplication();
        }
    }
}