namespace Femur.Logging.AdvancedExample;

public class WorkerOptions
{
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxRetries { get; set; } = 3;
    public bool EnableValidation { get; set; } = true;
}
