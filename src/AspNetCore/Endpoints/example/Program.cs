using Femur;
using Femur.AspNetCore.Endpoints.Example;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<MyEndpoint>();

var app = builder.Build();

app.MapEndpoint<MyEndpoint>("/instance", [HttpMethod.Get], x => x.HandleAsync);

app.MapGet("/standard", async ([FromServices] MyEndpoint ep, [FromQuery] string? f, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    await ep.HandleAsync(f, httpContext, cancellationToken);
});

app.Run();
