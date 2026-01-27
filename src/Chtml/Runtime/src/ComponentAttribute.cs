using System;

namespace Femur.Chtml.Runtime;

/// <summary>
/// Marks a class as a reusable component.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ComponentAttribute : Attribute
{
}
