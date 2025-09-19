namespace Femur.Hosting;

/// <summary>
/// Defines the contract for a console application's main execution service.
/// </summary>
public interface IConsoleApplication
{
    /// <summary>
    /// Executes the main logic of the console application.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that indicates when the application should stop.</param>
    /// <returns>
    /// A task that represents the asynchronous execution of the console application.
    /// The task result should contain the exit code (0 for success, non-zero for errors).
    /// </returns>
    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}