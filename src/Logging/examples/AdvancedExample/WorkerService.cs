using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Femur.Logging.AdvancedExample;

public class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;
    private readonly IWorkItemValidator _validator;
    private readonly IWorkItemProcessor _processor;
    private readonly WorkerOptions _options;
    private readonly ActivitySource _activitySource;
    private int _workItemCounter;

    public WorkerService(
        ILogger<WorkerService> logger,
        IWorkItemValidator validator,
        IWorkItemProcessor processor,
        IOptions<WorkerOptions> options,
        ActivitySource activitySource)
    {
        this._logger = logger;
        this._validator = validator;
        this._processor = processor;
        this._options = options.Value;
        this._activitySource = activitySource;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("Worker service started with interval: {Interval}", this._options.ProcessingInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await this.ProcessNextWorkItem(stoppingToken);
                await Task.Delay(this._options.ProcessingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            this._logger.LogInformation("Worker service stopping due to cancellation");
        }
        catch (Exception ex)
        {
            this._logger.LogCritical(ex, "Fatal error in worker service");
            throw;
        }
        finally
        {
            this._logger.LogInformation("Worker service stopped");
        }
    }

    private async Task ProcessNextWorkItem(CancellationToken cancellationToken)
    {
        using var activity = this._activitySource.StartActivity("ProcessWorkItemWithRetries", ActivityKind.Internal);

        var workItem = this.GenerateWorkItem();

        activity?.SetTag("workitem.id", workItem.Id);
        activity?.SetTag("workitem.description", workItem.Description);
        activity?.SetTag("retry.max_attempts", this._options.MaxRetries);
        activity?.SetTag("validation.enabled", this._options.EnableValidation);

        this._logger.LogDebug("Generated work item: {@WorkItem}", workItem);

        // Validate if enabled
        if (this._options.EnableValidation)
        {
            if (!this._validator.Validate(workItem, out var error))
            {
                this._logger.LogWarning("Work item {Id} failed validation: {Error}", workItem.Id, error);
                activity?.SetStatus(ActivityStatusCode.Error, "Validation failed");
                activity?.SetTag("validation.failed", true);
                return;
            }

            activity?.SetTag("validation.passed", true);
        }

        // Process with retries
        var attempt = 0;
        while (attempt < this._options.MaxRetries)
        {
            attempt++;
            activity?.SetTag("retry.current_attempt", attempt);

            try
            {
                var success = await this._processor.ProcessAsync(workItem, cancellationToken);

                if (success)
                {
                    if (attempt > 1)
                    {
                        this._logger.LogInformation(
                            "Work item {Id} processed successfully after {Attempts} attempts",
                            workItem.Id, attempt);
                    }

                    activity?.SetStatus(ActivityStatusCode.Ok);
                    activity?.SetTag("processing.success", true);
                    activity?.SetTag("retry.final_attempt", attempt);
                    return;
                }

                if (attempt < this._options.MaxRetries)
                {
                    this._logger.LogWarning(
                        "Work item {Id} processing failed, retrying ({Attempt}/{MaxRetries})...",
                        workItem.Id, attempt, this._options.MaxRetries);
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
                throw;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Unexpected error processing work item {Id} (attempt {Attempt})",
                    workItem.Id, attempt);

                if (attempt >= this._options.MaxRetries)
                {
                    this._logger.LogError("Work item {Id} failed after {MaxRetries} attempts",
                        workItem.Id, this._options.MaxRetries);
                    activity?.SetStatus(ActivityStatusCode.Error, "All retries exhausted");
                    activity?.SetTag("processing.success", false);
                    activity?.SetTag("retry.exhausted", true);
                }
            }
        }
    }

    private WorkItem GenerateWorkItem()
    {
        this._workItemCounter++;

        // Occasionally generate invalid items for testing
        var random = new Random();
        if (random.Next(100) < 10)
        {
            return new WorkItem(-1, "", DateTime.UtcNow);
        }

        return new WorkItem(
            this._workItemCounter,
            $"Process task #{this._workItemCounter}",
            DateTime.UtcNow);
    }
}
