using Microsoft.AspNetCore.Mvc;

namespace Femur.AspNetCore.Endpoints.Example;

public class MyEndpoint
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    public MyEndpoint(IConfiguration configuration, ILogger<MyEndpoint> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync([FromQuery] string? f, HttpContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("I am a log");
        await context.Response.WriteAsync($"Hello there tomatos and {f ?? "{{empty}}"}");
    }
}