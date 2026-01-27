

using System;

namespace Femur.Chtml.Runtime;

/// <summary>
/// Marks a class as a routable page component.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RouteAttribute : Attribute
{
    public string Path { get; }

    public RouteAttribute(string path)
    {
        this.Path = path;
    }
}
