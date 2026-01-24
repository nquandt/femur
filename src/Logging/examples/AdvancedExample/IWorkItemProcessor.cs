namespace Femur.Logging.AdvancedExample;

public interface IWorkItemProcessor
{
    Task<bool> ProcessAsync(WorkItem item, CancellationToken cancellationToken);
}
