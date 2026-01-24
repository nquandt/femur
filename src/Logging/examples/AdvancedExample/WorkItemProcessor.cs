using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Femur.Logging.AdvancedExample;

public class WorkItemProcessor : IWorkItemProcessor
{
    private readonly ILogger<WorkItemProcessor> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Random _random = new();

    public WorkItemProcessor(ILogger<WorkItemProcessor> logger, ActivitySource activitySource)
    {
        this._logger = logger;
        this._activitySource = activitySource;
    }

    public async Task<bool> ProcessAsync(WorkItem item, CancellationToken cancellationToken)
    {
        using var activity = this._activitySource.StartActivity("ProcessWorkItem", ActivityKind.Internal);
        activity?.SetTag("workitem.id", item.Id);
        activity?.SetTag("workitem.description", item.Description);
        activity?.AddEvent(new ActivityEvent("ProcessingStarted"));

        this._logger.LogInformation("Processing work item {Id}: {Description}", item.Id, item.Description);

        try
        {
            // Simulate processing with random delays
            var delay = this._random.Next(100, 500);
            activity?.SetTag("processing.delay_ms", delay);
            await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);

            // Simulate occasional failures (20% chance)
            if (this._random.Next(100) < 20)
            {
                throw new InvalidOperationException($"Simulated processing error for work item {item.Id}");
            }

            this._logger.LogInformation("Successfully processed work item {Id}", item.Id);
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("processing.result", "success");
            activity?.AddEvent(new ActivityEvent("ProcessingCompleted"));
            return true;
        }
        catch (OperationCanceledException)
        {
            this._logger.LogWarning("Processing cancelled for work item {Id}", item.Id);
            activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
            activity?.SetTag("processing.result", "cancelled");
            throw;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to process work item {Id}", item.Id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("processing.result", "failed");
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.AddEvent(new ActivityEvent("ProcessingFailed"));
            return false;
        }
    }
}
