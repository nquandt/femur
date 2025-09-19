using Femur.Hosting.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

return await Femur.Hosting.ApplicationBuilder.Create(args)
    .UseLogging(c =>
    {
        c.ClearProviders();
        c.AddConsole();
        c.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices(services => { })
    .ConfigureAsWebApplication(app =>
    {
        app.UseRouting();

        app.MapGet("/", () => "Hello World!");
    })
    .RunAsync();