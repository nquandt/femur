namespace Femur.Hosting;

/// <summary>
/// Final executable builder interface that only allows running the application.
/// </summary>
public interface IExecutableApplicationBuilder
{
    /// <summary>
    /// Builds and runs the console application.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the exit code.
    /// </returns>
    Task<int> RunAsync();

    /// <summary>
    /// Builds and runs the console application with a specific IConsoleApplication implementation.
    /// </summary>
    /// <typeparam name="TApplication">The IConsoleApplication implementation to run.</typeparam>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the exit code.
    /// </returns>
    Task<int> RunAsync<TApplication>() where TApplication : class, IConsoleApplication;
}