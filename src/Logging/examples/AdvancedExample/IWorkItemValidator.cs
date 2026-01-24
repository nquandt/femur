namespace Femur.Logging.AdvancedExample;

public interface IWorkItemValidator
{
    bool Validate(WorkItem item, out string? error);
}
