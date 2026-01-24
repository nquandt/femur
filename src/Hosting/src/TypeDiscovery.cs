using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HostingTests")]

namespace Femur.Hosting;

/// <summary>
/// Internal utility for discovering the Program type using reflection.
/// Used by the hosting framework to determine appropriate logger categories.
/// </summary>
internal static class TypeDiscovery
{
    private static readonly Lazy<Type> ProgramType = new(DiscoverProgramType);
    private static readonly Lazy<string> LoggerCategoryName = new(() => GetLoggerCategoryName(ProgramType.Value));

    /// <summary>
    /// Discovers the Program class type using reflection by looking for the entry point.
    /// </summary>
    /// <returns>The Type of the Program class, or a fallback type if not found.</returns>
    internal static Type DiscoverProgramType()
    {
        try
        {
            // First, try to get the entry assembly
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                // Look for a class named "Program" first
                var programType = entryAssembly.GetType("Program") ??
                                entryAssembly.GetTypes().FirstOrDefault(t => t.Name == "Program");

                if (programType != null)
                {
                    return programType;
                }

                // If no Program class found, try to find the entry point method
                var entryPoint = entryAssembly.EntryPoint;
                if (entryPoint?.DeclaringType != null)
                {
                    return entryPoint.DeclaringType;
                }
            }

            // Fallback: try to find Program class in calling assembly
            var callingAssembly = Assembly.GetCallingAssembly();
            if (callingAssembly != null)
            {
                var programType = callingAssembly.GetType("Program") ??
                                callingAssembly.GetTypes().FirstOrDefault(t => t.Name == "Program");

                if (programType != null)
                {
                    return programType;
                }
            }

            // Final fallback: use the stack trace to find the calling type
            var stackTrace = new StackTrace();
            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame?.GetMethod();
                if (method?.DeclaringType != null
                    && method.DeclaringType.Name == "Program"
                    && method.Name == "Main")
                {
                    return method.DeclaringType;
                }
            }
        }
        catch
        {
            // If discovery fails, fall back to a generic type
        }

        // Ultimate fallback
        return typeof(object);
    }

    /// <summary>
    /// Gets an appropriate logger category name from the specified type.
    /// </summary>
    /// <param name="programType">The program type to derive the category from.</param>
    /// <returns>A logger category name derived from the type's namespace or assembly.</returns>
    internal static string GetLoggerCategoryName(Type programType)
    {
        // If we have a namespace, use it (e.g., "MyApp.Program" -> "MyApp")
        if (!string.IsNullOrEmpty(programType.Namespace))
        {
            // If the namespace ends with the type name, use the namespace minus the type name
            var namespaceParts = programType.Namespace.Split('.');
            if (namespaceParts.Length > 1 && namespaceParts.Last() == programType.Name)
            {
                return string.Join(".", namespaceParts.Take(namespaceParts.Length - 1));
            }

            return programType.Namespace;
        }

        // Fallback to assembly name without extensions
        var assemblyName = programType.Assembly.GetName().Name;
        return assemblyName ?? "Application";
    }

    /// <summary>
    /// Gets the logger category name for the auto-discovered program type.
    /// </summary>
    /// <returns>A logger category name derived from the auto-discovered program type.</returns>
    internal static string GetAutoDiscoveredLoggerCategoryName()
    {
        return LoggerCategoryName.Value;
    }

    /// <summary>
    /// Gets the auto-discovered Program type.
    /// </summary>
    /// <returns>The discovered Program type.</returns>
    internal static Type GetDiscoveredProgramType()
    {
        return ProgramType.Value;
    }
}
