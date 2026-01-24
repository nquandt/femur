using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Femur.Logging.AdvancedExample;

public class WorkItemValidator : IWorkItemValidator
{
    private readonly ILogger<WorkItemValidator> _logger;
    private readonly ActivitySource _activitySource;

    public WorkItemValidator(ILogger<WorkItemValidator> logger, ActivitySource activitySource)
    {
        this._logger = logger;
        this._activitySource = activitySource;
    }

    public bool Validate(WorkItem item, out string? error)
    {
        using var activity = this._activitySource.StartActivity("ValidateWorkItem", ActivityKind.Internal);
        activity?.SetTag("workitem.id", item.Id);
        activity?.SetTag("workitem.description", item.Description);

        this._logger.LogDebug("Validating work item {Id}", item.Id);

        if (item.Id <= 0)
        {
            error = "Work item ID must be positive";
            activity?.SetStatus(ActivityStatusCode.Error, error);
            activity?.SetTag("validation.result", "failed");
            activity?.SetTag("validation.error", error);
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Description))
        {
            error = "Work item description cannot be empty";
            activity?.SetStatus(ActivityStatusCode.Error, error);
            activity?.SetTag("validation.result", "failed");
            activity?.SetTag("validation.error", error);
            return false;
        }

        if (item.CreatedAt > DateTime.UtcNow)
        {
            error = "Work item creation date cannot be in the future";
            activity?.SetStatus(ActivityStatusCode.Error, error);
            activity?.SetTag("validation.result", "failed");
            activity?.SetTag("validation.error", error);
            return false;
        }

        error = null;
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("validation.result", "success");
        return true;
    }
}
