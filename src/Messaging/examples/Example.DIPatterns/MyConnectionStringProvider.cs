using Microsoft.Extensions.Configuration;

namespace Femur.Messaging.Example.DIPatterns;

public class MyConnectionStringProvider : IConnectionStringProvider
{
    private readonly IConfiguration _configuration;

    public MyConnectionStringProvider(IConfiguration configuration)
    {
        this._configuration = configuration;
    }

    public string GetConnectionString(string name)
    {
        return this._configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' not found");
    }
}
