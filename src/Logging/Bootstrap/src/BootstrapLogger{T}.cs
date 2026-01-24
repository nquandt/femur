using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Femur.Logging.Bootstrap;

public class BootstrapLogger<T> : BootstrapLogger
{
    internal BootstrapLogger(ServiceCollection bootstrappedServices, IServiceProvider serviceProvider)
        : base(bootstrappedServices, serviceProvider, new Lazy<ILogger>(() => serviceProvider.GetRequiredService<ILogger<T>>()))
    {
    }
}
