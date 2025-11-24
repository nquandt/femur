using Femur.Hosting.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

return await Femur.Hosting.ApplicationBuilder.Create(args)
    .UseLogging(c =>
    {
        _ = c.ClearProviders();
        _ = c.AddConsole();
        _ = c.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureConfiguration(config =>
    {
        _ = config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _ = config.AddEnvironmentVariables();
    })
    .ConfigureServices(services => { })
    .ConfigureAsWebApplication(app =>
    {
        _ = app.UseRouting();

        _ = app.MapGet("/", () => "Hello World!");
    })
    .RunAsync();
