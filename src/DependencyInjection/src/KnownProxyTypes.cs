using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Femur.DependencyInjection;

/// <summary>
/// Registry of known proxy types for common open generic interfaces.
/// These are hand-written for better performance and debuggability.
/// </summary>
internal static class KnownProxyTypes
{
    private static readonly Dictionary<Type, Type> KnownTypes = new()
    {
        [typeof(ILogger<>)] = typeof(ProxiedLogger<>),
        [typeof(IOptions<>)] = typeof(ProxiedOptions<>),
        [typeof(IOptionsSnapshot<>)] = typeof(ProxiedOptionsSnapshot<>),
        [typeof(IOptionsMonitor<>)] = typeof(ProxiedOptionsMonitor<>),
    };

    public static bool TryGetProxyType(Type openGenericService, out Type proxyType)
    {
        return KnownTypes.TryGetValue(openGenericService, out proxyType!);
    }
}
