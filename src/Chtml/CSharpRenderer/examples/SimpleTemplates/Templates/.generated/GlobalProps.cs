namespace Templates.Generated;

/// <summary>
/// Global properties available to all templates during rendering.
/// These properties are set at the request level and available throughout the render tree.
/// Generated from global.chtml
/// </summary>
public class GlobalProps
{
    public System.String? Language { get; set; }
    public required System.String SiteName { get; set; }
    public required System.String Theme { get; set; }
}
