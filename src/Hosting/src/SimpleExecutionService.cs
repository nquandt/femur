namespace Femur.Hosting;

/// <summary>
/// A simple console application implementation that executes a user-provided function.
/// This is used internally by RunAsync methods to provide a simplified console application API
/// as an alternative to implementing IConsoleApplication directly.
/// </summary>
internal class SimpleExecutionService : IConsoleApplication
{
    private readonly Func<IServiceProvider, CancellationToken, Task<int>> _execute;
    private readonly IServiceProvider _serviceProvider;

    public SimpleExecutionService(
        Func<IServiceProvider, CancellationToken, Task<int>> execute,
        IServiceProvider serviceProvider)
    {
        this._execute = execute;
        this._serviceProvider = serviceProvider;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        return await this._execute(this._serviceProvider, cancellationToken);
    }
}