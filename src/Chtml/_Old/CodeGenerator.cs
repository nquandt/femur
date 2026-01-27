using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tokenizer;
using YamlDotNet.RepresentationModel;
using ChtmlCompiler;

namespace ChtmlCompiler;

/// <summary>
/// Generates C# code from CHTML AST nodes.
/// Converts parsed HTML templates into renderer classes implementing IRenderable<T>.
/// </summary>
public static class CodeGenerator
{
    /// <summary>
    /// Generates C# code for a component or page from its AST and metadata.
    /// </summary>
    /// <param name="document">The parsed document AST</param>
    /// <param name="className">The name of the component/page class</param>
    /// <param name="namespace">The namespace for the generated class</param>
    /// <param name="frontMatter">The front matter dictionary</param>
    /// <param name="isComponent">Whether this is a component (true) or page (false)</param>
    /// <param name="route">The route for pages (only used if isComponent is false)</param>
    /// <param name="markdownFiles">Output parameter: list of markdown file paths referenced via LoadMarkdown</param>
    /// <returns>Generated C# code</returns>
    public static string Generate(
        DocumentNode document,
        string className,
        string @namespace,
        Dictionary<string, object>? frontMatter,
        bool isComponent,
        string? route = null,
        List<(string id, string content)>? allScripts = null,
        List<(string id, string content)>? allStyles = null,
        string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance",
        List<(string name, string type, bool isNullable)>? globalPropsInfo = null,
        string? relativePath = null,
        Dictionary<string, string>? globalComponents = null,
        HashSet<string>? markdownFiles = null,
        Dictionary<string, string>? markdownClassMap = null,
        string? templatesRoot = null)
    {
        var sb = new StringBuilder();

        // Parse props from front matter
        var inputProps = ParseProps(frontMatter, "Props");
        var computedProps = ParseProps(frontMatter, "ComputedProps");
        var vars = ParseVars(frontMatter, relativePath, markdownFiles);
        var hasInputProps = inputProps.Any();
        var hasComputedProps = computedProps.Any();
        var hasVars = vars.Any();
        
        // Determine props types
        // If computed props exist, we need separate InputProps and Props classes
        // If no computed props, we use a single Props class (no InputProps needed)
        string inputPropsType, propsType;
        if (hasComputedProps)
        {
            // Has computed props: need both InputProps and Props (with inheritance)
            inputPropsType = hasInputProps ? $"{className}InputProps" : "EmptyPropsInstance";
            propsType = $"{className}Props";
        }
        else if (hasInputProps)
        {
            // No computed props: just use Props (same as InputProps)
            inputPropsType = $"{className}Props";
            propsType = $"{className}Props";
        }
        else
        {
            // No props at all
            inputPropsType = "EmptyPropsInstance";
            propsType = "EmptyPropsInstance";
        }
        
        // Only needs transformation if we have computed props
        var needsTransformation = hasComputedProps;

        // Generate using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Shared.Meta;");
        sb.AppendLine("using Shared.Generated;");
        sb.AppendLine("using Templates.Generated;");
        
        // Add Components namespace if this is a page (so it can reference components)
        if (!isComponent)
        {
            sb.AppendLine("using Templates.Generated.Components;");
        }
        
        sb.AppendLine();

        // Generate namespace
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();

        // Generate props classes if needed
        if (hasComputedProps)
        {
            // When computed props exist, generate both InputProps and Props (with inheritance)
            if (hasInputProps)
            {
                GeneratePropsClass(sb, $"{className}InputProps", inputProps, null, false, globalPropsTypeName);
                sb.AppendLine();
            }
            GeneratePropsClass(sb, $"{className}Props", inputProps, computedProps, hasInputProps, globalPropsTypeName);
            sb.AppendLine();
        }
        else if (hasInputProps)
        {
            // No computed props: just generate Props class (no InputProps needed)
            GeneratePropsClass(sb, $"{className}Props", inputProps, null, false, globalPropsTypeName);
            sb.AppendLine();
        }

        // Generate component or page class
        // Always generate as partial so users can add TransformProps implementation in code-beside
        if (isComponent)
        {
            // Component: implements IRenderable<TInputProps, TGlobalProps>
            // Note: Class is partial to allow code-beside TransformProps implementation
            sb.AppendLine($"public partial class {className} : IRenderable<{inputPropsType}, {globalPropsTypeName}>");
        }
        else
        {
            // Page: implements IRenderablePage<TInputProps, TGlobalProps>
            // Note: Class is partial to allow code-beside TransformProps implementation
            sb.AppendLine($"public partial class {className} : IRenderablePage<{inputPropsType}, {globalPropsTypeName}>");
        }
        sb.AppendLine("{");

        // Collect component dependencies from AST (before using them)
        var dependencies = new HashSet<string>();
        CollectDependencies(document, dependencies, relativePath, globalComponents);

        // Generate Route property for pages
        if (!isComponent)
        {
            var routeValue = route ?? "/";
            sb.AppendLine($"    public static string Route => \"{EscapeString(routeValue)}\";");
            sb.AppendLine();
        }

        // Generate DependsOn method
        GenerateDependsOnMethod(sb, dependencies, isComponent);

        // Generate TransformProps method if transformation is needed
        // This is an internal implementation detail - not part of the interface contract
        // Users MUST override this in a code-beside file (e.g., ComponentName.partial.cs) when ComputedProps exist
        if (needsTransformation)
        {
            GenerateTransformPropsMethod(sb, className, inputPropsType, propsType, inputProps, computedProps, globalPropsTypeName);
        }

        // Generate RenderAsync method
        // RenderAsync accepts TInputProps (what callers pass) and transforms internally if needed
        sb.AppendLine($"    public static async ValueTask RenderAsync(RenderContext<{globalPropsTypeName}> renderContext, {inputPropsType} inputProps, params RenderPipe<{globalPropsTypeName}>[] children)");
        sb.AppendLine("    {");
        sb.AppendLine("        var (writer, globalProps) = renderContext;");
        sb.AppendLine();
        
        // Transform props if needed (components with computed props)
        if (needsTransformation)
        {
            sb.AppendLine($"        var props = TransformProps(inputProps, globalProps);");
            sb.AppendLine();
        }
        else
        {
            // No transformation needed - use inputProps directly as props
            sb.AppendLine($"        var props = inputProps;");
            sb.AppendLine();
        }

        // Build vars map for code block resolution (before GenerateVars so it can populate varsTypeMap)
        var varsMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var varsTypeMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (varName, varValue, isFunction, varType) in vars)
        {
            varsMap.Add(varName);
            if (varType != null)
            {
                varsTypeMap[varName] = varType;
            }
            else if (isFunction)
            {
                // If no explicit type, check if it's a function call that returns RenderPipe
                // For LoadMarkdown, we know it returns RenderPipe
                if (varValue != null && varValue.Contains("LoadMarkdown", StringComparison.OrdinalIgnoreCase))
                {
                    varsTypeMap[varName] = "RenderPipe<GlobalProps>";
                }
            }
        }

        // Generate vars if present
        if (hasVars)
        {
            GenerateVars(sb, vars, globalPropsTypeName, markdownClassMap, relativePath, varsTypeMap, templatesRoot);
            sb.AppendLine();
        }

        // Build props map for code block resolution
        var propsMap = new Dictionary<string, ComponentPropInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in inputProps)
        {
            propsMap[prop.Name] = prop;
        }
        if (hasComputedProps)
        {
            foreach (var prop in computedProps)
            {
                propsMap[prop.Name] = prop;
            }
        }

        // Generate rendering code for document children
        foreach (var child in document.Children)
        {
            GenerateNodeRendering(sb, child, 2, allScripts ?? new List<(string id, string content)>(), allStyles ?? new List<(string id, string content)>(), propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, new HashSet<string>(), templatesRoot);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Collects component dependencies from the AST.
    /// Only collects direct dependencies (components used directly via ComponentNode).
    /// Resolves component names the same way as GenerateComponentRendering.
    /// </summary>
    private static void CollectDependencies(HtmlNode node, HashSet<string> dependencies, string? relativePath = null, Dictionary<string, string>? globalComponents = null)
    {
        if (node is ComponentNode component)
        {
            var rawComponentName = component.ComponentName;
            string resolvedFullPath;
            
            // Resolve component name the same way as GenerateComponentRendering
            if (rawComponentName.StartsWith("."))
            {
                // Relative component: .ComponentName or ./ComponentName
                // Remove leading . and / characters
                var relativeName = rawComponentName.TrimStart('.').TrimStart('/');
                if (string.IsNullOrEmpty(relativeName))
                {
                    // Invalid: just "." or "./" - skip this dependency
                    return;
                }
                var resolvedComponentName = ToPascalCase(relativeName);
                
                // For relative imports, use the current component's namespace
                // Extract namespace from relativePath (e.g., components/header/index.chtml -> Templates.Generated.Components.Header)
                if (!string.IsNullOrEmpty(relativePath))
                {
                    var segments = relativePath.Split('/');
                    var componentsIndex = -1;
                    
                    for (int i = 0; i < segments.Length; i++)
                    {
                        if (segments[i].Equals("components", StringComparison.OrdinalIgnoreCase))
                        {
                            componentsIndex = i;
                            break;
                        }
                    }
                    
                    if (componentsIndex >= 0)
                    {
                        var nsParts = new List<string> { "Templates", "Generated", "Components" };
                        
                        // Add folder names after "components" up to (but not including) the file name
                        for (int i = componentsIndex + 1; i < segments.Length - 1; i++)
                        {
                            var segment = segments[i];
                            segment = RemoveBracketNotationForComponent(segment);
                            var cleaned = System.Text.RegularExpressions.Regex.Replace(segment, "[^A-Za-z0-9]", "_");
                            if (!string.IsNullOrEmpty(cleaned))
                            {
                                nsParts.Add(StringUtils.ToPascalCase(cleaned));
                            }
                        }
                        
                        var currentNamespace = string.Join(".", nsParts);
                        resolvedFullPath = $"{currentNamespace}.{resolvedComponentName}";
                    }
                    else
                    {
                        // Fallback if we can't determine namespace
                        resolvedFullPath = $"Templates.Generated.Components.{resolvedComponentName}";
                    }
                }
                else
                {
                    // Fallback if no relativePath
                    resolvedFullPath = $"Templates.Generated.Components.{resolvedComponentName}";
                }
            }
            else if (rawComponentName.Contains("."))
            {
                // Fully qualified: Namespace.ComponentName
                var parts = rawComponentName.Split('.');
                var namespacePart = parts[0];
                var componentPart = parts[parts.Length - 1];
                var resolvedComponentName = ToPascalCase(componentPart);
                resolvedFullPath = $"Templates.Generated.Components.{namespacePart}.{resolvedComponentName}";
            }
            else
            {
                // Check if it's a global component
                if (globalComponents != null && globalComponents.TryGetValue(rawComponentName, out var globalComponentPath))
                {
                    resolvedFullPath = globalComponentPath;
                }
                else
                {
                    // Default: assume it's in Components namespace
                    var resolvedComponentName = ToPascalCase(rawComponentName);
                    resolvedFullPath = $"Templates.Generated.Components.{resolvedComponentName}";
                }
            }
            
            dependencies.Add(resolvedFullPath);
        }

        foreach (var child in node.Children)
        {
            CollectDependencies(child, dependencies, relativePath, globalComponents);
        }
    }

    /// <summary>
    /// Generates the DependsOn() method that returns direct component dependencies.
    /// </summary>
    private static void GenerateDependsOnMethod(StringBuilder sb, HashSet<string> dependencies, bool isComponent)
    {
        sb.AppendLine("    public static Type[] DependsOn()");
        sb.AppendLine("    {");
        
        if (dependencies.Count == 0)
        {
            sb.AppendLine("        return Array.Empty<Type>();");
        }
        else
        {
            // Sort dependencies for consistent output
            var sortedDeps = dependencies.OrderBy(d => d).ToList();
            sb.AppendLine("        return new Type[]");
            sb.AppendLine("        {");
            
            foreach (var dep in sortedDeps)
            {
                // Dependencies are stored as namespace paths (e.g., "Templates.Generated.Components.Container")
                // For components in nested namespaces, we need to append the class name (typically "Index" for index files)
                // Components in nested namespaces have more than 3 parts: Templates.Generated.Components.{Folder}
                var parts = dep.Split('.');
                string depWithClassName;
                
                if (parts.Length > 3 && !dep.EndsWith(".Index") && !dep.EndsWith(".Stub"))
                {
                    // Path ends with a namespace segment (folder name), append "Index" (most common case for index files)
                    depWithClassName = $"{dep}.Index";
                }
                else if (parts.Length == 3)
                {
                    // Flat namespace (Templates.Generated.Components.ComponentName) - this shouldn't happen with new structure
                    // But handle it by appending the last part as class name
                    depWithClassName = $"{dep}.{parts[parts.Length - 1]}";
                }
                else
                {
                    // Already has class name or is a global component path
                    depWithClassName = dep;
                }
                
                sb.AppendLine($"            typeof({depWithClassName}),");
            }
            
            sb.AppendLine("        };");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Parses props from front matter YAML.
    /// </summary>
    /// <param name="frontMatter">The front matter dictionary</param>
    /// <param name="sectionName">The section name to parse ("Props" or "ComputedProps")</param>
    private static List<ComponentPropInfo> ParseProps(Dictionary<string, object>? frontMatter, string sectionName)
    {
        var props = new List<ComponentPropInfo>();
        
        if (frontMatter == null || !frontMatter.TryGetValue(sectionName, out var propsObj))
            return props;

        if (propsObj is Dictionary<string, object> propsDict)
        {
            foreach (var (name, typeObj) in propsDict)
            {
                var typeStr = typeObj?.ToString() ?? "System.String";
                
                // Check if this is a function call (should be in Vars, not Props)
                if (typeStr.StartsWith("function::", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip function calls in Props - they should be in Vars or ComputedProps
                    continue;
                }
                
                var (typeName, isNullable) = ParseType(typeStr);
                props.Add(new ComponentPropInfo
                {
                    Name = name,
                    TypeName = typeName,
                    IsNullable = isNullable
                });
            }
        }

        return props;
    }

    /// <summary>
    /// Parses vars from front matter.
    /// Supports function calls like function::LoadMarkdown("./file.md")
    /// </summary>
    public static List<(string name, string value, bool isFunction, string? varType)> ParseVars(Dictionary<string, object>? frontMatter, string? relativePath, HashSet<string>? markdownFiles = null, HashSet<string>? contentFolders = null, HashSet<string>? collectionMarkdownFiles = null)
    {
        var vars = new List<(string name, string value, bool isFunction, string? varType)>();
        
        if (frontMatter == null || !frontMatter.TryGetValue("Vars", out var varsObj))
            return vars;

        if (varsObj is Dictionary<string, object> varsDict)
        {
            foreach (var (name, valueObj) in varsDict)
            {
                var valueStr = valueObj?.ToString() ?? "";
                var isFunction = valueStr.StartsWith("function::", StringComparison.OrdinalIgnoreCase);
                string? varType = null;
                
                if (isFunction)
                {
                    // Extract markdown file path from LoadMarkdown calls
                    if (valueStr.Contains("LoadMarkdown(") && markdownFiles != null)
                    {
                        var openParen = valueStr.IndexOf('(');
                        var closeParen = valueStr.LastIndexOf(')');
                        if (openParen != -1 && closeParen != -1)
                        {
                            var argsStr = valueStr.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                            // Remove quotes
                            if ((argsStr.StartsWith("\"") && argsStr.EndsWith("\"")) ||
                                (argsStr.StartsWith("'") && argsStr.EndsWith("'")))
                            {
                                argsStr = argsStr.Substring(1, argsStr.Length - 2);
                            }
                            // Normalize path relative to templates root
                            // If path starts with ./, resolve relative to current template file's directory
                            string normalizedPath;
                            if (argsStr.StartsWith("./"))
                            {
                                // Relative to current template file
                                if (!string.IsNullOrEmpty(relativePath))
                                {
                                    var templateDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                                    if (!string.IsNullOrEmpty(templateDir))
                                    {
                                        normalizedPath = $"{templateDir}/{argsStr.Substring(2)}";
                                    }
                                    else
                                    {
                                        normalizedPath = argsStr.Substring(2);
                                    }
                                }
                                else
                                {
                                    normalizedPath = argsStr.Substring(2);
                                }
                            }
                            else
                            {
                                normalizedPath = argsStr.TrimStart('.').TrimStart('/');
                            }
                            markdownFiles.Add(normalizedPath);
                        }
                    }
                    
                    // Extract type from function call for LoadAsCollection
                    if (valueStr.Contains("LoadAsCollection<"))
                    {
                        var genericStart = valueStr.IndexOf('<');
                        var genericEnd = valueStr.IndexOf('>', genericStart);
                        if (genericStart != -1 && genericEnd != -1)
                        {
                            var typeName = valueStr.Substring(genericStart + 1, genericEnd - genericStart - 1).Trim();
                            varType = $"System.Collections.Generic.IEnumerable<{typeName}>";
                        }
                    }
                    // Extract type from function call for LoadMarkdown<TProps>
                    else if (valueStr.Contains("LoadMarkdown<"))
                    {
                        var genericStart = valueStr.IndexOf('<');
                        var genericEnd = valueStr.IndexOf('>', genericStart);
                        if (genericStart != -1 && genericEnd != -1)
                        {
                            var typeName = valueStr.Substring(genericStart + 1, genericEnd - genericStart - 1).Trim();
                            varType = typeName; // LoadMarkdown<TProps> returns TProps instance
                            
                            // Track _content folders for props generation
                            if (contentFolders != null)
                            {
                                // Extract file path(s) from LoadMarkdown arguments
                                var openParen = valueStr.IndexOf('(');
                                var closeParen = valueStr.LastIndexOf(')');
                                if (openParen != -1 && closeParen != -1)
                                {
                                    var argsStr = valueStr.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                                    // Parse arguments (could be multiple paths separated by commas)
                                    var arguments = ParseArguments(argsStr);
                                    
                                    foreach (var arg in arguments)
                                    {
                                        // Remove quotes
                                        var path = arg.Trim();
                                        if ((path.StartsWith("\"") && path.EndsWith("\"")) ||
                                            (path.StartsWith("'") && path.EndsWith("'")))
                                        {
                                            path = path.Substring(1, path.Length - 2);
                                        }
                                        
                                        // Check if path references _content folder
                                        if (path.Contains("_content", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Normalize path relative to templates root
                                            string normalizedPath;
                                            if (path.StartsWith("./"))
                                            {
                                                if (!string.IsNullOrEmpty(relativePath))
                                                {
                                                    var templateDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                                                    if (!string.IsNullOrEmpty(templateDir))
                                                    {
                                                        normalizedPath = $"{templateDir}/{path.Substring(2)}";
                                                    }
                                                    else
                                                    {
                                                        normalizedPath = path.Substring(2);
                                                    }
                                                }
                                                else
                                                {
                                                    normalizedPath = path.Substring(2);
                                                }
                                            }
                                            else
                                            {
                                                normalizedPath = path.TrimStart('.').TrimStart('/');
                                            }
                                            
                                            // Extract _content folder path (remove filename, keep directory)
                                            // Handle paths like "./_content/{globalProps.Language}.md" -> "pages/about/_content"
                                            // or "./_content/en.md" -> "pages/about/_content"
                                            var contentFolderPath = normalizedPath;
                                            
                                            // Remove filename pattern (e.g., "{globalProps.Language}.md" or "en.md")
                                            var lastSlash = contentFolderPath.LastIndexOf('/');
                                            if (lastSlash >= 0)
                                            {
                                                contentFolderPath = contentFolderPath.Substring(0, lastSlash);
                                            }
                                            
                                            // Verify it ends with _content and add the full path
                                            if (contentFolderPath.EndsWith("_content", StringComparison.OrdinalIgnoreCase))
                                            {
                                                // Use the full path including parent directories
                                                contentFolders.Add(contentFolderPath);
                                            }
                                            else if (contentFolderPath.Contains("/_content/", StringComparison.OrdinalIgnoreCase))
                                            {
                                                // Extract just the _content folder path including parent directories
                                                var contentIndex = contentFolderPath.IndexOf("/_content/", StringComparison.OrdinalIgnoreCase);
                                                if (contentIndex >= 0)
                                                {
                                                    // Get everything up to and including "/_content"
                                                    var folderPath = contentFolderPath.Substring(0, contentIndex + "/_content/".Length - 1); // -1 to remove trailing /
                                                    contentFolders.Add(folderPath);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // Extract type from function call for LoadMarkdownCollectionByLanguage<TProps>
                    else if (valueStr.Contains("LoadMarkdownCollectionByLanguage<"))
                    {
                        var genericStart = valueStr.IndexOf('<');
                        var genericEnd = valueStr.IndexOf('>', genericStart);
                        if (genericStart != -1 && genericEnd != -1)
                        {
                            var typeName = valueStr.Substring(genericStart + 1, genericEnd - genericStart - 1).Trim();
                            varType = typeName; // LoadMarkdownCollectionByLanguage<TProps> returns TProps[] instance
                            
                            // Track _content folders and markdown files for props generation
                            if (contentFolders != null || markdownFiles != null || collectionMarkdownFiles != null)
                            {
                                // Extract glob pattern from LoadMarkdownCollectionByLanguage arguments
                                var openParen = valueStr.IndexOf('(');
                                var closeParen = valueStr.LastIndexOf(')');
                                if (openParen != -1 && closeParen != -1)
                                {
                                    var argsStr = valueStr.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                                    var arguments = ParseArguments(argsStr);
                                    
                                    if (arguments.Count > 0)
                                    {
                                        // First argument is the glob pattern (e.g., "./_content/**/{lang}.md")
                                        var pattern = arguments[0].Trim();
                                        if ((pattern.StartsWith("\"") && pattern.EndsWith("\"")) ||
                                            (pattern.StartsWith("'") && pattern.EndsWith("'")))
                                        {
                                            pattern = pattern.Substring(1, pattern.Length - 2);
                                        }
                                        
                                        // Check if pattern references _content folder
                                        if (pattern.Contains("_content", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Normalize pattern relative to templates root
                                            string normalizedPattern;
                                            if (pattern.StartsWith("./"))
                                            {
                                                if (!string.IsNullOrEmpty(relativePath))
                                                {
                                                    var templateDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                                                    if (!string.IsNullOrEmpty(templateDir))
                                                    {
                                                        normalizedPattern = $"{templateDir}/{pattern.Substring(2)}";
                                                    }
                                                    else
                                                    {
                                                        normalizedPattern = pattern.Substring(2);
                                                    }
                                                }
                                                else
                                                {
                                                    normalizedPattern = pattern.Substring(2);
                                                }
                                            }
                                            else
                                            {
                                                normalizedPattern = pattern.TrimStart('.').TrimStart('/');
                                            }
                                            
                                            // Extract _content folder path (remove glob patterns and {lang})
                                            var contentIndex = normalizedPattern.IndexOf("/_content/", StringComparison.OrdinalIgnoreCase);
                                            if (contentIndex >= 0)
                                            {
                                                var folderPath = normalizedPattern.Substring(0, contentIndex + "/_content/".Length - 1);
                                                if (contentFolders != null)
                                                {
                                                    contentFolders.Add(folderPath);
                                                }
                                            }
                                            else if (normalizedPattern.EndsWith("_content", StringComparison.OrdinalIgnoreCase) || 
                                                     normalizedPattern.StartsWith("_content", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var folderPath = normalizedPattern.Split('/').TakeWhile(p => !p.Contains("*") && !p.Contains("{lang}")).Aggregate((a, b) => $"{a}/{b}");
                                                if (contentFolders != null && !string.IsNullOrEmpty(folderPath))
                                                {
                                                    contentFolders.Add(folderPath);
                                                }
                                            }
                                            
                                            // Track the pattern for later scanning (we'll scan and generate classes in Program.cs)
                                            // Store the normalized pattern so we can scan for files later
                                            if (collectionMarkdownFiles != null)
                                            {
                                                // We'll use a special marker to indicate this is a pattern to scan
                                                // The actual file scanning will happen in Program.cs
                                                collectionMarkdownFiles.Add($"PATTERN:{normalizedPattern}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // Extract type from function call for LoadMarkdownByLanguage<TProps>
                    else if (valueStr.Contains("LoadMarkdownByLanguage<"))
                    {
                        var genericStart = valueStr.IndexOf('<');
                        var genericEnd = valueStr.IndexOf('>', genericStart);
                        if (genericStart != -1 && genericEnd != -1)
                        {
                            var typeName = valueStr.Substring(genericStart + 1, genericEnd - genericStart - 1).Trim();
                            varType = typeName; // LoadMarkdownByLanguage<TProps> returns TProps instance
                            
                            // Track _content folders for props generation
                            if (contentFolders != null)
                            {
                                // Extract folder path from LoadMarkdownByLanguage arguments
                                var openParen = valueStr.IndexOf('(');
                                var closeParen = valueStr.LastIndexOf(')');
                                if (openParen != -1 && closeParen != -1)
                                {
                                    var argsStr = valueStr.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                                    var arguments = ParseArguments(argsStr);
                                    
                                    if (arguments.Count > 0)
                                    {
                                        // First argument is the folder path
                                        var path = arguments[0].Trim();
                                        if ((path.StartsWith("\"") && path.EndsWith("\"")) ||
                                            (path.StartsWith("'") && path.EndsWith("'")))
                                        {
                                            path = path.Substring(1, path.Length - 2);
                                        }
                                        
                                        // Check if path references _content folder
                                        if (path.Contains("_content", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Normalize path relative to templates root
                                            string normalizedPath;
                                            if (path.StartsWith("./"))
                                            {
                                                if (!string.IsNullOrEmpty(relativePath))
                                                {
                                                    var templateDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                                                    if (!string.IsNullOrEmpty(templateDir))
                                                    {
                                                        normalizedPath = $"{templateDir}/{path.Substring(2)}";
                                                    }
                                                    else
                                                    {
                                                        normalizedPath = path.Substring(2);
                                                    }
                                                }
                                                else
                                                {
                                                    normalizedPath = path.Substring(2);
                                                }
                                            }
                                            else
                                            {
                                                normalizedPath = path.TrimStart('.').TrimStart('/');
                                            }
                                            
                                            // Verify it ends with _content
                                            if (normalizedPath.EndsWith("_content", StringComparison.OrdinalIgnoreCase))
                                            {
                                                contentFolders.Add(normalizedPath);
                                            }
                                            else if (normalizedPath.Contains("/_content/", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var contentIndex = normalizedPath.IndexOf("/_content/", StringComparison.OrdinalIgnoreCase);
                                                if (contentIndex >= 0)
                                                {
                                                    var folderPath = normalizedPath.Substring(0, contentIndex + "/_content/".Length - 1);
                                                    contentFolders.Add(folderPath);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                vars.Add((name, valueStr, isFunction, varType));
            }
        }

        return vars;
    }

    /// <summary>
    /// Generates code for vars in the RenderAsync method.
    /// </summary>
    private static void GenerateVars(StringBuilder sb, List<(string name, string value, bool isFunction, string? varType)> vars, string globalPropsTypeName, Dictionary<string, string>? markdownClassMap = null, string? relativePath = null, Dictionary<string, string?>? varsTypeMap = null, string? templatesRoot = null)
    {
        foreach (var (name, value, isFunction, varType) in vars)
        {
            if (isFunction)
            {
                // Parse function call: function::MethodName(arg1, arg2, ...)
                var functionCall = ParseFunctionCall(value, globalPropsTypeName, markdownClassMap, relativePath, templatesRoot);
                
                // Check if the result is a RenderAsync method group (RenderPipe delegate)
                if (functionCall.Contains(".RenderAsync") && !functionCall.Contains("("))
                {
                    // This is a method group assignment, which is a RenderPipe delegate
                    if (varsTypeMap != null)
                    {
                        varsTypeMap[name] = "RenderPipe<GlobalProps>";
                    }
                }
                
                sb.AppendLine($"        var {name} = {functionCall};");
            }
            else
            {
                // Static value - treat as string literal
                var escapedValue = EscapeStringForRegularLiteral(value);
                sb.AppendLine($"        var {name} = \"{escapedValue}\";");
            }
        }
    }

    /// <summary>
    /// Parses a function call like "function::LoadMarkdown(\"./file.md\")" or "function::LoadAsCollection<Type>(\"./pattern.md\")" and generates C# code.
    /// For LoadMarkdown, references the generated static markdown class instead of calling a runtime function.
    /// For LoadMarkdownByLanguage, generates compile-time language switching code.
    /// </summary>
    private static string ParseFunctionCall(string functionCall, string globalPropsTypeName, Dictionary<string, string>? markdownClassMap = null, string? relativePath = null, string? templatesRoot = null)
    {
        // Remove "function::" prefix
        var call = functionCall.Substring("function::".Length).Trim();
        
        // Check for generic type parameters: MethodName<Type>(args)
        string functionName;
        string? genericType = null;
        var genericStart = call.IndexOf('<');
        var openParen = call.IndexOf('(');
        
        if (genericStart != -1 && (openParen == -1 || genericStart < openParen))
        {
            // Has generic type parameter
            functionName = call.Substring(0, genericStart).Trim();
            var genericEnd = call.IndexOf('>', genericStart);
            if (genericEnd != -1)
            {
                genericType = call.Substring(genericStart + 1, genericEnd - genericStart - 1).Trim();
                // Remove the generic part from call for further processing
                call = functionName + call.Substring(genericEnd + 1);
            }
        }
        else
        {
            // No generic type parameter
            functionName = call.Substring(0, openParen == -1 ? call.Length : openParen).Trim();
        }
        
        // Extract arguments
        openParen = call.IndexOf('(');
        if (openParen == -1)
        {
            // No arguments
            if (genericType != null)
            {
                return $"Templates.Generated.TemplateFunctions.{functionName}<{genericType}>(globalProps)";
            }
            return $"Templates.Generated.TemplateFunctions.{functionName}(globalProps)";
        }
        
        var argsStr = call.Substring(openParen + 1);
        
        // Remove closing parenthesis
        if (argsStr.EndsWith(")"))
        {
            argsStr = argsStr.Substring(0, argsStr.Length - 1).Trim();
        }
        
        // Handle LoadMarkdownByLanguage specially - generate compile-time language switching
        if (functionName.Equals("LoadMarkdownByLanguage", StringComparison.OrdinalIgnoreCase))
        {
            if (genericType == null)
            {
                return "/* ERROR: LoadMarkdownByLanguage requires a generic type parameter */ null";
            }
            
            // Parse arguments: folder path and language property access
            var langArgs = ParseArguments(argsStr);
            if (langArgs.Count < 2)
            {
                return "/* ERROR: LoadMarkdownByLanguage requires folder path and language */ null";
            }
            
            var folderPath = langArgs[0].Trim();
            // Remove quotes
            if ((folderPath.StartsWith("\"") && folderPath.EndsWith("\"")) ||
                (folderPath.StartsWith("'") && folderPath.EndsWith("'")))
            {
                folderPath = folderPath.Substring(1, folderPath.Length - 2);
            }
            
            // Normalize folder path relative to templates root
            string normalizedFolderPath;
            if (folderPath.StartsWith("./"))
            {
                if (!string.IsNullOrEmpty(relativePath))
                {
                    var templateDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                    if (!string.IsNullOrEmpty(templateDir))
                    {
                        normalizedFolderPath = $"{templateDir}/{folderPath.Substring(2)}";
                    }
                    else
                    {
                        normalizedFolderPath = folderPath.Substring(2);
                    }
                }
                else
                {
                    normalizedFolderPath = folderPath.Substring(2);
                }
            }
            else
            {
                normalizedFolderPath = folderPath.TrimStart('.').TrimStart('/');
            }
            
            // Extract language variable placeholder from path pattern (e.g., "{lang}" in "./_content/{lang}.md")
            // The language variable location is where {lang} appears in the path
            var langPlaceholder = "{lang}";
            var langPlaceholderIndex = normalizedFolderPath.IndexOf(langPlaceholder, StringComparison.OrdinalIgnoreCase);
            if (langPlaceholderIndex == -1)
            {
                return $"/* ERROR: LoadMarkdownByLanguage path must contain {{lang}} placeholder. Found: {folderPath} */ null";
            }
            
            // Get language property access (e.g., "globalProps.Language")
            var languageAccess = langArgs[1].Trim();
            
            // Scan _content folder for language files at compile time
            if (string.IsNullOrEmpty(templatesRoot))
            {
                return $"/* ERROR: templatesRoot not provided for LoadMarkdownByLanguage */ null";
            }
            
            // Check if path contains runtime placeholders like {props.Slug}
            var propsPlaceholderPattern = @"\{props\.(\w+)\}";
            var propsMatch = System.Text.RegularExpressions.Regex.Match(normalizedFolderPath, propsPlaceholderPattern);
            
            if (propsMatch.Success)
            {
                // Path contains a runtime placeholder (e.g., {props.Slug})
                // We need to generate a nested switch: first by props, then by language
                var propsPlaceholder = propsMatch.Value; // e.g., "{props.Slug}"
                var propsProperty = propsMatch.Groups[1].Value; // e.g., "Slug"
                var propsAccess = $"props.{propsProperty}"; // e.g., "props.Slug"
                
                // Extract parts around the props placeholder
                var beforeProps = normalizedFolderPath.Substring(0, propsMatch.Index);
                var afterProps = normalizedFolderPath.Substring(propsMatch.Index + propsMatch.Length);
                
                // Extract parts around the lang placeholder (relative to afterProps)
                var langPlaceholderInAfterProps = afterProps.IndexOf(langPlaceholder, StringComparison.OrdinalIgnoreCase);
                if (langPlaceholderInAfterProps == -1)
                {
                    return $"/* ERROR: LoadMarkdownByLanguage path must contain {{lang}} placeholder after props placeholder. Found: {folderPath} */ null";
                }
                
                var beforeLang = beforeProps + afterProps.Substring(0, langPlaceholderInAfterProps);
                var afterLang = afterProps.Substring(langPlaceholderInAfterProps + langPlaceholder.Length);
                
                // Normalize beforeLang (remove trailing slash if present, as Path.Combine handles it)
                beforeLang = beforeLang.TrimEnd('/', '\\');
                
                // Build base search directory (the _content folder)
                var baseSearchDir = Path.Combine(templatesRoot, beforeLang);
                if (!Directory.Exists(baseSearchDir))
                {
                    return $"/* ERROR: Content folder not found: {baseSearchDir} (normalized from: {beforeLang}) */ null";
                }
                
                // Find all subdirectories in the _content folder (these are the slug values)
                var subdirectories = Directory.GetDirectories(baseSearchDir, "*", SearchOption.TopDirectoryOnly)
                    .Select(d => Path.GetFileName(d))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .OrderBy(d => d)
                    .ToList();
                
                if (subdirectories.Count == 0)
                {
                    return $"/* ERROR: No subdirectories found in {baseSearchDir} */ null";
                }
                
                // Build nested switch statement: props.Slug switch { ... }
                var sb = new StringBuilder();
                sb.Append($"{propsAccess} switch {{ ");
                
                foreach (var subdir in subdirectories)
                {
                    // For each subdirectory, find language files
                    var subdirPath = Path.Combine(baseSearchDir, subdir);
                    var searchPattern = "*" + afterLang;
                    var matchingFiles = Directory.GetFiles(subdirPath, searchPattern, SearchOption.TopDirectoryOnly)
                        .Select(f => Path.GetRelativePath(templatesRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
                        .ToList();
                    
                    // Extract language codes
                    var languageFiles = new List<string>();
                    var subdirBeforeLang = beforeLang + "/" + subdir + "/";
                    foreach (var file in matchingFiles)
                    {
                        if (file.StartsWith(subdirBeforeLang) && file.EndsWith(afterLang))
                        {
                            var langCode = file.Substring(subdirBeforeLang.Length, file.Length - subdirBeforeLang.Length - afterLang.Length);
                            if (!string.IsNullOrEmpty(langCode) && !languageFiles.Contains(langCode))
                            {
                                languageFiles.Add(langCode);
                            }
                        }
                    }
                    
                    languageFiles = languageFiles.OrderBy(f => f).ToList();
                    
                    if (languageFiles.Count == 0)
                        continue; // Skip subdirectories with no language files
                    
                    // Default fallback language
                    var fallbackLang = languageFiles.FirstOrDefault(l => l.Equals("en", StringComparison.OrdinalIgnoreCase)) ?? languageFiles[0];
                    
                    // Generate inner switch for language selection
                    sb.Append($"\"{subdir}\" => {languageAccess} switch {{ ");
                    
                    foreach (var lang in languageFiles)
                    {
                        // Skip fallback language (handled by default case)
                        if (lang.Equals(fallbackLang, StringComparison.OrdinalIgnoreCase))
                            continue;
                        
                        var filePath = subdirBeforeLang + lang + afterLang;
                        
                        // Look up the generated static class name
                        string? className = null;
                        if (markdownClassMap != null)
                        {
                            if (markdownClassMap.TryGetValue(filePath, out className))
                            {
                                // Found exact match
                            }
                            else
                            {
                                // Try case-insensitive lookup
                                var matchingKey = markdownClassMap.Keys.FirstOrDefault(k => 
                                    k.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                                if (matchingKey != null)
                                {
                                    className = markdownClassMap[matchingKey];
                                }
                            }
                        }
                        
                        if (className != null)
                        {
                            sb.Append($"\"{lang}\" => {className}.GetContent(), ");
                        }
                    }
                    
                    // Default fallback case for this subdirectory
                    var fallbackPath = subdirBeforeLang + fallbackLang + afterLang;
                    string? fallbackClassFullName = null;
                    if (markdownClassMap != null)
                    {
                        if (markdownClassMap.TryGetValue(fallbackPath, out fallbackClassFullName))
                        {
                            // Found exact match
                        }
                        else
                        {
                            // Try case-insensitive lookup
                            var matchingKey = markdownClassMap.Keys.FirstOrDefault(k => 
                                k.Equals(fallbackPath, StringComparison.OrdinalIgnoreCase));
                            if (matchingKey != null)
                            {
                                fallbackClassFullName = markdownClassMap[matchingKey];
                            }
                        }
                    }
                    
                    if (fallbackClassFullName != null)
                    {
                        sb.Append($"_ => {fallbackClassFullName}.GetContent()");
                    }
                    else
                    {
                        sb.Append($"_ => null");
                    }
                    
                    sb.Append(" }, ");
                }
                
                // Default case for unknown slug
                sb.Append($"_ => null");
                sb.Append(" }");
                return sb.ToString();
            }
            else
            {
                // No runtime placeholders - use the original logic
                // Extract directory and filename pattern
                var beforeLang = normalizedFolderPath.Substring(0, langPlaceholderIndex);
                var afterLang = normalizedFolderPath.Substring(langPlaceholderIndex + langPlaceholder.Length);
                
                // Build search directory and pattern
                var searchDir = Path.Combine(templatesRoot, beforeLang);
                if (!Directory.Exists(searchDir))
                {
                    return $"/* ERROR: Content folder not found: {searchDir} */ null";
                }
                
                // Create search pattern: replace {lang} with * to find all language files
                var searchPattern = "*" + afterLang;
                
                // Find all matching files
                var matchingFiles = Directory.GetFiles(searchDir, searchPattern, SearchOption.TopDirectoryOnly)
                    .Select(f => Path.GetRelativePath(templatesRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
                    .ToList();
                
                // Extract language codes from file paths (where {lang} was)
                var languageFiles = new List<string>();
                foreach (var file in matchingFiles)
                {
                    // Extract the language code from the file path where {lang} placeholder was
                    if (file.StartsWith(beforeLang) && file.EndsWith(afterLang))
                    {
                        var langCode = file.Substring(beforeLang.Length, file.Length - beforeLang.Length - afterLang.Length);
                        if (!string.IsNullOrEmpty(langCode) && !languageFiles.Contains(langCode))
                        {
                            languageFiles.Add(langCode);
                        }
                    }
                }
                
                languageFiles = languageFiles.OrderBy(f => f).ToList();
                
                if (languageFiles.Count == 0)
                {
                    return $"/* ERROR: No markdown files found matching pattern {normalizedFolderPath} in {searchDir} */ null";
                }
                
                // Default fallback to first language (or "en" if available, otherwise first)
                var fallbackLang = languageFiles.FirstOrDefault(l => l.Equals("en", StringComparison.OrdinalIgnoreCase)) ?? languageFiles[0];
                var fallbackPath = beforeLang + fallbackLang + afterLang;
                
                string? fallbackClassFullName = null;
                if (markdownClassMap != null)
                {
                    if (markdownClassMap.TryGetValue(fallbackPath, out fallbackClassFullName))
                    {
                        // Found exact match
                    }
                    else
                    {
                        // Try case-insensitive lookup
                        var matchingKey = markdownClassMap.Keys.FirstOrDefault(k => 
                            k.Equals(fallbackPath, StringComparison.OrdinalIgnoreCase));
                        if (matchingKey != null)
                        {
                            fallbackClassFullName = markdownClassMap[matchingKey];
                        }
                    }
                }
                
                // Generate switch statement for language selection
                // Use the language access expression from the template (e.g., "globalProps.Language")
                // Note: We use static classes generated at compile time, not runtime LoadMarkdown calls
                var sb = new StringBuilder();
                sb.Append($"{languageAccess} switch {{ ");
                
                foreach (var lang in languageFiles)
                {
                    // Skip "en" if it's the fallback language (will be handled by default case)
                    if (lang.Equals(fallbackLang, StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    // Construct file path by replacing {lang} with actual language code
                    var filePath = beforeLang + lang + afterLang;
                    
                    // Look up the generated static class name
                    // Try both exact match and case-insensitive match
                    string? className = null;
                    if (markdownClassMap != null)
                    {
                        // Try exact match first
                        if (markdownClassMap.TryGetValue(filePath, out className))
                        {
                            // Found exact match
                        }
                        else
                        {
                            // Try case-insensitive lookup
                            var matchingKey = markdownClassMap.Keys.FirstOrDefault(k => 
                                k.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                            if (matchingKey != null)
                            {
                                className = markdownClassMap[matchingKey];
                            }
                        }
                    }
                    
                    if (className != null)
                    {
                        // Reference the static class's GetContent method
                        sb.Append($"\"{lang}\" => {className}.GetContent(), ");
                    }
                    else
                    {
                        // Fallback: use runtime LoadMarkdown (should not happen if map is populated correctly)
                        sb.Append($"\"{lang}\" => Templates.Generated.TemplateFunctions.LoadMarkdown<{genericType}>(\"{filePath}\", null, globalProps), ");
                    }
                }
                
                // Default fallback case
                if (fallbackClassFullName != null)
                {
                    sb.Append($"_ => {fallbackClassFullName}.GetContent()");
                }
                else
                {
                    var fallbackClass = GenerateMarkdownClassName(fallbackPath);
                    sb.Append($"_ => Templates.Generated.Markdown.{fallbackClass}.GetContent()");
                }
                
                sb.Append(" }");
                return sb.ToString();
            }
        }
        
        // Handle LoadMarkdownCollectionByLanguage - returns an array of markdown files matching a pattern, filtered by language
        if (functionName.Equals("LoadMarkdownCollectionByLanguage", StringComparison.OrdinalIgnoreCase))
        {
            if (genericType == null)
            {
                return "/* ERROR: LoadMarkdownCollectionByLanguage requires a generic type parameter */ null";
            }
            
            // Parse arguments: glob pattern and language property access
            var langArgs = ParseArguments(argsStr);
            if (langArgs.Count < 2)
            {
                return "/* ERROR: LoadMarkdownCollectionByLanguage requires glob pattern and language */ null";
            }
            
            var globPattern = langArgs[0].Trim();
            // Remove quotes
            if ((globPattern.StartsWith("\"") && globPattern.EndsWith("\"")) ||
                (globPattern.StartsWith("'") && globPattern.EndsWith("'")))
            {
                globPattern = globPattern.Substring(1, globPattern.Length - 2);
            }
            
            // Normalize glob pattern relative to templates root
            string normalizedPattern;
            if (globPattern.StartsWith("./"))
            {
                if (!string.IsNullOrEmpty(relativePath))
                {
                    var templateDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                    if (!string.IsNullOrEmpty(templateDir))
                    {
                        normalizedPattern = $"{templateDir}/{globPattern.Substring(2)}";
                    }
                    else
                    {
                        normalizedPattern = globPattern.Substring(2);
                    }
                }
                else
                {
                    normalizedPattern = globPattern.Substring(2);
                }
            }
            else
            {
                normalizedPattern = globPattern.TrimStart('.').TrimStart('/');
            }
            
            // Extract language variable placeholder from pattern (e.g., "{lang}" in "./_content/**/{lang}.md")
            var langPlaceholder = "{lang}";
            var langPlaceholderIndex = normalizedPattern.IndexOf(langPlaceholder, StringComparison.OrdinalIgnoreCase);
            if (langPlaceholderIndex == -1)
            {
                return $"/* ERROR: LoadMarkdownCollectionByLanguage pattern must contain {{lang}} placeholder. Found: {globPattern} */ null";
            }
            
            // Get language property access (e.g., "globalProps.Language")
            var languageAccess = langArgs[1].Trim();
            
            // Scan for matching files at compile time
            if (string.IsNullOrEmpty(templatesRoot))
            {
                return $"/* ERROR: templatesRoot not provided for LoadMarkdownCollectionByLanguage */ null";
            }
            
            // Extract base directory (before ** or {lang})
            var beforeLang = normalizedPattern.Substring(0, langPlaceholderIndex);
            var afterLang = normalizedPattern.Substring(langPlaceholderIndex + langPlaceholder.Length);
            
            // Check if pattern contains ** (recursive search)
            var isRecursive = normalizedPattern.Contains("**");
            
            // Build search directory
            var searchBaseDir = beforeLang;
            if (searchBaseDir.Contains("**"))
            {
                // Remove ** from path - we'll search recursively
                searchBaseDir = searchBaseDir.Replace("**", "").TrimEnd('/');
            }
            
            var searchDir = Path.Combine(templatesRoot, searchBaseDir);
            if (!Directory.Exists(searchDir))
            {
                return $"/* ERROR: Content folder not found: {searchDir} */ null";
            }
            
            // Find all matching files recursively
            var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allMdFiles = Directory.GetFiles(searchDir, "*.md", searchOption)
                .Select(f => Path.GetRelativePath(templatesRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
                .ToList();
            
            // Group files by language code (extracted from where {lang} appears in pattern)
            var filesByLanguage = new Dictionary<string, List<string>>();
            
            foreach (var file in allMdFiles)
            {
                // Check if file matches the pattern structure
                // Pattern: pages/experience/_content/**/{lang}.md
                // File: pages/experience/_content/01/en.md
                // We need to extract the language code from the filename (where {lang} appears)
                
                // Remove the base directory to get relative path from searchDir
                var relativeFile = Path.GetRelativePath(searchDir, Path.Combine(templatesRoot, file))
                    .Replace(Path.DirectorySeparatorChar, '/');
                
                // The language code is the filename without extension (where {lang} appears in pattern)
                var fileName = Path.GetFileName(file);
                var langCode = Path.GetFileNameWithoutExtension(fileName);
                
                // Verify the file structure matches: it should be in a subdirectory of _content
                // and the filename should match the pattern after {lang}
                if (file.Contains("_content") && file.EndsWith(afterLang))
                {
                    if (!filesByLanguage.ContainsKey(langCode))
                    {
                        filesByLanguage[langCode] = new List<string>();
                    }
                    filesByLanguage[langCode].Add(file);
                }
            }
            
            if (filesByLanguage.Count == 0)
            {
                return $"/* ERROR: No markdown files found matching pattern {normalizedPattern} */ System.Array.Empty<{genericType}>()";
            }
            
            // Default fallback: return files for first available language (or "en" if available)
            var fallbackLang = filesByLanguage.Keys.FirstOrDefault(l => l.Equals("en", StringComparison.OrdinalIgnoreCase)) ?? filesByLanguage.Keys.First();
            var fallbackFiles = filesByLanguage[fallbackLang].OrderBy(f => f).ToList();
            
            // Generate code that returns an array based on language
            var sb = new StringBuilder();
            sb.Append($"{languageAccess} switch {{ ");
            
            foreach (var langGroup in filesByLanguage.OrderBy(kvp => kvp.Key))
            {
                var lang = langGroup.Key;
                
                // Skip "en" if it's the fallback language (will be handled by default case)
                if (lang.Equals(fallbackLang, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                var files = langGroup.Value.OrderBy(f => f).ToList();
                
                sb.Append($"\"{lang}\" => new[] {{ ");
                
                foreach (var file in files)
                {
                    // Look up the generated static class name
                    string? className = null;
                    if (markdownClassMap != null)
                    {
                        if (markdownClassMap.TryGetValue(file, out className))
                        {
                            // Found exact match
                        }
                        else
                        {
                            // Try case-insensitive lookup
                            var matchingKey = markdownClassMap.Keys.FirstOrDefault(k => 
                                k.Equals(file, StringComparison.OrdinalIgnoreCase));
                            if (matchingKey != null)
                            {
                                className = markdownClassMap[matchingKey];
                            }
                        }
                    }
                    
                    if (className != null)
                    {
                        sb.Append($"{className}.GetContent(), ");
                    }
                    else
                    {
                        // Fallback: use runtime LoadMarkdown
                        sb.Append($"Templates.Generated.TemplateFunctions.LoadMarkdown<{genericType}>(\"{file}\", null, globalProps), ");
                    }
                }
                
                sb.Append("}, ");
            }
            
            // Default fallback case
            sb.Append("_ => new[] { ");
            foreach (var file in fallbackFiles)
            {
                string? className = null;
                if (markdownClassMap != null)
                {
                    if (markdownClassMap.TryGetValue(file, out className))
                    {
                        // Found exact match
                    }
                    else
                    {
                        var matchingKey = markdownClassMap.Keys.FirstOrDefault(k => 
                            k.Equals(file, StringComparison.OrdinalIgnoreCase));
                        if (matchingKey != null)
                        {
                            className = markdownClassMap[matchingKey];
                        }
                    }
                }
                
                if (className != null)
                {
                    sb.Append($"{className}.GetContent(), ");
                }
                else
                {
                    sb.Append($"Templates.Generated.TemplateFunctions.LoadMarkdown<{genericType}>(\"{file}\", null, globalProps), ");
                }
            }
            sb.Append("} ");
            
            sb.Append("}");
            return sb.ToString();
        }
        
        // Handle LoadMarkdown specially
        if (functionName.Equals("LoadMarkdown", StringComparison.OrdinalIgnoreCase))
        {
            // If generic type is provided, use runtime LoadMarkdown<TProps> method
            if (genericType != null)
            {
                // Parse arguments - support multiple file paths (primary and fallback)
                var markdownArgs = ParseArguments(argsStr);
                var markdownArgsCode = "";
                if (markdownArgs.Count > 0)
                {
                    // First argument is primary path
                    markdownArgsCode = markdownArgs[0];
                }
                // Second argument (if present) is fallback path
                if (markdownArgs.Count > 1)
                {
                    markdownArgsCode += $", {markdownArgs[1]}";
                }
                else
                {
                    // No fallback - pass null
                    markdownArgsCode += ", null";
                }
                markdownArgsCode += ", globalProps";
                return $"Templates.Generated.TemplateFunctions.LoadMarkdown<{genericType}>({markdownArgsCode})";
            }
            
            // No generic type - use generated static class (legacy behavior)
            // Extract file path from arguments
            var filePath = argsStr.Trim();
            // Remove quotes
            if ((filePath.StartsWith("\"") && filePath.EndsWith("\"")) ||
                (filePath.StartsWith("'") && filePath.EndsWith("'")))
            {
                filePath = filePath.Substring(1, filePath.Length - 2);
            }
            
            // Normalize path relative to templates root (same logic as ParseVars)
            string normalizedPath;
            if (filePath.StartsWith("./"))
            {
                // Relative to current template file
                if (!string.IsNullOrEmpty(relativePath))
                {
                    var templateDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                    if (!string.IsNullOrEmpty(templateDir))
                    {
                        normalizedPath = $"{templateDir}/{filePath.Substring(2)}";
                    }
                    else
                    {
                        normalizedPath = filePath.Substring(2);
                    }
                }
                else
                {
                    normalizedPath = filePath.Substring(2);
                }
            }
            else
            {
                normalizedPath = filePath.TrimStart('.').TrimStart('/');
            }
            
            // Look up the generated class name (includes namespace)
            if (markdownClassMap != null && markdownClassMap.TryGetValue(normalizedPath, out var className))
            {
                // Return reference to the generated static class's RenderAsync method
                // className already includes full namespace (e.g., "Templates.Generated.Pages.Home")
                // RenderAsync method group can be directly assigned to RenderPipe delegate
                return $"{className}.RenderAsync";
            }
            
            // Fallback: if not found in map, generate a class name from the path
            var fallbackClassName = GenerateMarkdownClassName(normalizedPath);
            return $"Templates.Generated.Markdown.{fallbackClassName}.RenderAsync";
        }
        
        // Parse arguments (simple string arguments for now)
        var arguments = ParseArguments(argsStr);
        
        // Build function call - always pass globalProps as last argument
        var argsCode = string.Join(", ", arguments);
        if (!string.IsNullOrEmpty(argsCode))
        {
            argsCode += ", ";
        }
        
        if (genericType != null)
        {
            return $"Templates.Generated.TemplateFunctions.{functionName}<{genericType}>({argsCode}globalProps)";
        }
        return $"Templates.Generated.TemplateFunctions.{functionName}({argsCode}globalProps)";
    }
    
    /// <summary>
    /// Parses function arguments, respecting quoted strings.
    /// </summary>
    private static List<string> ParseArguments(string argsStr)
    {
        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(argsStr))
        {
            // Simple parsing: split by comma, but respect quoted strings
            var currentArg = new StringBuilder();
            var inQuotes = false;
            var quoteChar = '\0';
            
            foreach (var c in argsStr)
            {
                if ((c == '"' || c == '\'') && !inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                    currentArg.Append(c);
                }
                else if (c == quoteChar && inQuotes)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                    currentArg.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    var arg = currentArg.ToString().Trim();
                    if (!string.IsNullOrEmpty(arg))
                    {
                        arguments.Add(arg);
                    }
                    currentArg.Clear();
                }
                else
                {
                    currentArg.Append(c);
                }
            }
            
            // Add last argument
            var lastArg = currentArg.ToString().Trim();
            if (!string.IsNullOrEmpty(lastArg))
            {
                arguments.Add(lastArg);
            }
        }
        
        return arguments;
    }
    
    /// <summary>
    /// Generates a class name from a markdown file path.
    /// Example: "pages/about/en.md" -> "PagesAboutEn"
    /// </summary>
    private static string GenerateMarkdownClassName(string filePath)
    {
        // Split by path separators and convert to PascalCase
        var parts = filePath.Replace('\\', '/').Split('/')
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Select(p => ToPascalCase(p))
            .ToArray();
        
        return string.Join("", parts);
    }

    /// <summary>
    /// Infers component type from props type name.
    /// Pattern: Components.{Category}.{Component}.{PropsType} -> Components.{Category}.{Component}.Index
    /// </summary>
    private static string? InferComponentTypeFromProps(string propsTypeName)
    {
        // Try to find the component type by namespace and name pattern
        var parts = propsTypeName.Split('.');
        
        // Look for Components namespace
        var componentsIndex = -1;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("Components", StringComparison.OrdinalIgnoreCase))
            {
                componentsIndex = i;
                break;
            }
        }
        
        // If no Components namespace found, try to infer from shorthand
        // Pattern: Experience.IndexProps -> Components.Experience.Index
        if (componentsIndex < 0 && parts.Length >= 2)
        {
            // Assume first part is category
            var category = parts[0];
            
            // Try: Templates.Generated.Components.{Category}.Index
            return $"Templates.Generated.Components.{category}.Index";
        }
        
        if (componentsIndex < 0) return null;
        
        // Build component type name: Components.{Category}.{Component}.Index
        // Remove the last part (usually InputProps or Props) and add Index
        var componentParts = new List<string>();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            componentParts.Add(parts[i]);
        }
        componentParts.Add("Index");
        
        var componentTypeName = string.Join(".", componentParts);
        
        // Try with Templates.Generated prefix
        return $"Templates.Generated.{componentTypeName}";
    }

    /// <summary>
    /// Parses a type string like "System.String | null" into type name and nullability.
    /// Normalizes common types like "RenderPipe" to their full namespace.
    /// </summary>
    private static (string typeName, bool isNullable) ParseType(string typeStr)
    {
        typeStr = typeStr.Trim();
        
        // Check for nullable syntax: System.String? or System.String | null
        var isNullable = false;
        string baseType;
        
        if (typeStr.EndsWith("?"))
        {
            // C# nullable syntax: System.String?
            baseType = typeStr.Substring(0, typeStr.Length - 1).Trim();
            isNullable = true;
        }
        else
        {
            // Check for | null syntax
            var parts = typeStr.Split('|', StringSplitOptions.TrimEntries);
            baseType = parts[0].Trim();
            isNullable = parts.Length > 1 && parts[1].Equals("null", StringComparison.OrdinalIgnoreCase);
        }
        
        // Normalize RenderPipe to full namespace
        if (baseType.Equals("RenderPipe", StringComparison.OrdinalIgnoreCase))
        {
            baseType = "Shared.Meta.RenderPipe";
        }
        
        return (baseType, isNullable);
    }

    /// <summary>
    /// Generates a props class for components with props.
    /// </summary>
    /// <param name="sb">StringBuilder to append to</param>
    /// <param name="className">Name of the props class</param>
    /// <param name="props">List of props to generate</param>
    /// <param name="computedProps">List of computed props (optional, for Props class that extends InputProps)</param>
    /// <param name="inheritFromInput">Whether this Props class should inherit from InputProps</param>
    private static void GeneratePropsClass(StringBuilder sb, string className, List<ComponentPropInfo> props, List<ComponentPropInfo>? computedProps = null, bool inheritFromInput = false, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance")
    {
        // If inheriting from InputProps, extract base class name
        if (inheritFromInput && computedProps != null && computedProps.Any())
        {
            var baseClassName = className.Replace("Props", "InputProps");
            sb.AppendLine($"public class {className} : {baseClassName}");
        }
        else
        {
            sb.AppendLine($"public class {className}");
        }
        
        sb.AppendLine("{");
        
        // Only generate input props if NOT inheriting from InputProps
        // When inheriting, the base class (InputProps) already contains all input props
        if (!inheritFromInput)
        {
            foreach (var prop in props)
            {
                var nullable = prop.IsNullable ? "?" : "";
                // Make non-nullable props required to avoid CS8618 warnings
                var required = prop.IsNullable ? "" : "required ";
                var typeName = NormalizeRenderPipeType(prop.TypeName, globalPropsTypeName);
                sb.AppendLine($"    public {required}{typeName}{nullable} {prop.Name} {{ get; set; }}");
            }
        }
        
        // Add computed props if provided
        if (computedProps != null)
        {
            // Track which props exist in the base class (when inheriting)
            var basePropsSet = inheritFromInput && props != null 
                ? new HashSet<string>(props.Select(p => p.Name), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var prop in computedProps)
            {
                // If this prop exists in the base class, we can't hide a required member
                // So we make it nullable and non-required, and use 'new' keyword to hide it
                // TransformProps will always set it, so it won't actually be null
                var isHidingBaseProp = basePropsSet.Contains(prop.Name);
                var nullable = (prop.IsNullable || isHidingBaseProp) ? "?" : "";
                // Can't use 'required' when hiding a base property
                var required = (prop.IsNullable || isHidingBaseProp) ? "" : "required ";
                var typeName = NormalizeRenderPipeType(prop.TypeName, globalPropsTypeName);
                
                // If this prop exists in the base class, use 'new' keyword to hide it
                var newKeyword = isHidingBaseProp ? "new " : "";
                
                sb.AppendLine($"    public {newKeyword}{required}{typeName}{nullable} {prop.Name} {{ get; set; }}");
            }
        }
        
        sb.AppendLine("}");
    }
    
    /// <summary>
    /// Normalizes RenderPipe type names to include the global props type parameter.
    /// </summary>
    private static string NormalizeRenderPipeType(string typeName, string globalPropsTypeName)
    {
        if (typeName == null) return typeName;
        
        var trimmed = typeName.Trim();
        // Check if this is a RenderPipe type (without generic parameter)
        if (trimmed.Equals("Shared.Meta.RenderPipe", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("RenderPipe", StringComparison.OrdinalIgnoreCase))
        {
            return $"RenderPipe<{globalPropsTypeName}>";
        }
        
        return typeName;
    }
    
    /// <summary>
    /// This MUST be implemented in a code-beside file (e.g., ComponentName.partial.cs).
    /// </summary>
    private static void GenerateTransformPropsMethod(StringBuilder sb, string className, string inputPropsType, string propsType, List<ComponentPropInfo> inputProps, List<ComponentPropInfo> computedProps, string globalPropsTypeName)
    {
        sb.AppendLine($"    // Partial method stub: MUST be implemented in code-beside file (e.g., {className}.partial.cs)");
        sb.AppendLine($"    private static partial {propsType} TransformProps({inputPropsType} inputProps, {globalPropsTypeName} globalProps);");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Gets the default value for a type.
    /// </summary>
    private static string GetDefaultValue(string typeName)
    {
        return typeName switch
        {
            "System.String" => "string.Empty",
            "System.Int32" => "0",
            "System.Boolean" => "false",
            "System.Double" => "0.0",
            _ => "default"
        };
    }

    /// <summary>
    /// Generates rendering code for a node and its children recursively.
    /// </summary>
    private static void GenerateNodeRendering(StringBuilder sb, HtmlNode node, int indentLevel, List<(string id, string content)> allScripts, List<(string id, string content)> allStyles, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, string? relativePath = null, Dictionary<string, string>? globalComponents = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null, HashSet<string>? usedVariableNames = null, string? templatesRoot = null)
    {
        var indent = new string(' ', indentLevel * 4);
        
        // Initialize variable name tracking if not provided
        if (usedVariableNames == null)
        {
            usedVariableNames = new HashSet<string>();
        }

        switch (node)
        {
            case DocumentNode doc:
                // Render all children of document
                foreach (var child in doc.Children)
                {
                    GenerateNodeRendering(sb, child, indentLevel, allScripts, allStyles, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, usedVariableNames, templatesRoot);
                }
                break;

            case ElementNode element:
                GenerateElementRendering(sb, element, indentLevel, allScripts, allStyles, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, usedVariableNames, templatesRoot);
                break;

            case TextNode text:
                GenerateTextRendering(sb, text, indentLevel, propsMap, globalPropsTypeName, globalPropsInfo, varsMap, varsTypeMap);
                break;

            case CommentNode comment:
                GenerateCommentRendering(sb, comment, indentLevel);
                break;

            case CodeNode code:
                GenerateCodeRendering(sb, code, indentLevel, allScripts, allStyles, propsMap, CodeBlockContext.TextContent, globalPropsTypeName, globalPropsInfo, varsMap, varsTypeMap);
                break;

            case ComponentNode component:
                GenerateComponentRendering(sb, component, indentLevel, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, templatesRoot);
                break;

            case DocumentTypeNode doctype:
                GenerateDoctypeRendering(sb, doctype, indentLevel);
                break;

            case CDataNode cdata:
                GenerateCDATARendering(sb, cdata, indentLevel);
                break;

            case ScriptNode script:
                GenerateScriptRendering(sb, script, indentLevel);
                break;

            case StyleNode style:
                GenerateStyleRendering(sb, style, indentLevel);
                break;

            case IfNode ifNode:
                GenerateIfRendering(sb, ifNode, indentLevel, allScripts, allStyles, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, usedVariableNames, templatesRoot);
                break;

            case ForNode forNode:
                GenerateForRendering(sb, forNode, indentLevel, allScripts, allStyles, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, usedVariableNames, templatesRoot);
                break;
        }
    }

    /// <summary>
    /// Processes a <code> element with Shiki syntax highlighting during compilation.
    /// Extracts code content, detects language, processes with Shiki, and outputs highlighted HTML.
    /// </summary>
    private static void ProcessCodeElement(StringBuilder sb, ElementNode codeElement, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        
        // Extract text content from children (code elements typically only have text children)
        var codeContent = new StringBuilder();
        foreach (var child in codeElement.Children)
        {
            if (child is TextNode textNode)
            {
                codeContent.Append(textNode.Content);
            }
            // Note: We don't process code blocks or directives inside <code> elements
            // They should be treated as literal text
        }
        
        var code = codeContent.ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            // Empty code element - output as-is
            var tagStr = BuildOpeningTag(codeElement);
            sb.AppendLine($"{indent}writer.Write(@\"{EscapeString(tagStr)}\");");
            if (!codeElement.IsSelfClosing)
            {
                sb.AppendLine($"{indent}writer.Write(@\"{EscapeString($"</{codeElement.TagName}>")}\n\");");
            }
            else
            {
                sb.AppendLine($"{indent}writer.Write(@\"\n\");");
            }
            return;
        }
        
        // Extract language from class attribute (e.g., "language-csharp" or "lang-csharp")
        // or from lang attribute
        string? language = null;
        if (codeElement.Attributes.TryGetValue("class", out var classValue))
        {
            // Look for language-* or lang-* pattern
            var match = System.Text.RegularExpressions.Regex.Match(classValue, @"(?:language|lang)-(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                language = match.Groups[1].Value;
            }
        }
        
        if (language == null && codeElement.Attributes.TryGetValue("lang", out var langValue))
        {
            language = langValue;
        }
        
        language ??= "text";
        
        // Process with Shiki during compilation
        var highlightedHtml = ShikiProcessor.HighlightCode(code, language);
        
        if (highlightedHtml != null)
        {
            // Shiki succeeded - output the highlighted HTML directly
            // Shiki already wraps it in <pre><code> tags, so we just output it
            var escapedHtml = EscapeString(highlightedHtml);
            sb.AppendLine($"{indent}writer.Write(@\"{escapedHtml}\n\");");
        }
        else
        {
            // Shiki not available or failed - fallback to plain code block
            var escapedCode = EscapeString(System.Net.WebUtility.HtmlEncode(code));
            var tagStr = BuildOpeningTag(codeElement);
            sb.AppendLine($"{indent}writer.Write(@\"{EscapeString(tagStr)}\");");
            sb.AppendLine($"{indent}writer.Write(@\"{escapedCode}\");");
            if (!codeElement.IsSelfClosing)
            {
                sb.AppendLine($"{indent}writer.Write(@\"{EscapeString($"</{codeElement.TagName}>")}\n\");");
            }
            else
            {
                sb.AppendLine($"{indent}writer.Write(@\"\n\");");
            }
        }
    }

    /// <summary>
    /// Generates rendering code for an HTML element.
    /// Handles code blocks in attribute values.
    /// </summary>
    private static void GenerateElementRendering(StringBuilder sb, ElementNode element, int indentLevel, List<(string id, string content)> allScripts, List<(string id, string content)> allStyles, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, string? relativePath = null, Dictionary<string, string>? globalComponents = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null, HashSet<string>? usedVariableNames = null, string? templatesRoot = null)
    {
        var indent = new string(' ', indentLevel * 4);
        
        // Special handling for <code> elements - process with Shiki during compilation
        if (string.Equals(element.TagName, "code", StringComparison.OrdinalIgnoreCase))
        {
            ProcessCodeElement(sb, element, indentLevel);
            return;
        }
        
        var hasChildren = element.Children.Count > 0;
        var hasCodeBlockAttributes = element.Attributes.Values.Any(IsCodeBlock);

        // If we have code blocks in attributes, use string interpolation
        if (hasCodeBlockAttributes)
        {
            GenerateOpeningTag(sb, element, indentLevel, propsMap, globalPropsTypeName, globalPropsInfo, varsMap, varsTypeMap, usedVariableNames);
        }
        else
        {
            // Simple static attributes - use verbatim string
            var tagStr = BuildOpeningTag(element);
            sb.AppendLine($"{indent}writer.Write(@\"{EscapeString(tagStr)}\n\");");
        }
        
        if (element.IsSelfClosing || element.IsVoidElement)
        {
            // Self-closing or void element - already done above
            return;
        }
        
        if (!hasChildren)
        {
            // Empty element with closing tag
            sb.AppendLine($"{indent}writer.Write(@\"{EscapeString($"</{element.TagName}>")}\n\");");
        }
        else
        {
            // Element with children - render children
                foreach (var child in element.Children)
                {
                    GenerateNodeRendering(sb, child, indentLevel + 1, allScripts, allStyles, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, usedVariableNames, templatesRoot);
                }

            // Closing tag
            sb.AppendLine($"{indent}writer.Write(@\"{EscapeString($"</{element.TagName}>")}\n\");");
        }
    }

    /// <summary>
    /// Generates the opening tag with attributes, handling code blocks in attribute values.
    /// Uses string interpolation when code blocks are present.
    /// </summary>
    private static void GenerateOpeningTag(StringBuilder sb, ElementNode element, int indentLevel, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null, HashSet<string>? usedVariableNames = null)
    {
        var indent = new string(' ', indentLevel * 4);
        
        // Initialize variable name tracking if not provided
        if (usedVariableNames == null)
        {
            usedVariableNames = new HashSet<string>();
        }
        
        // Collect code block attributes that need temporary variables
        var codeBlockAttrs = new List<(string name, string codeContent, string resolvedCode, string varName)>();
        var staticAttrs = new List<(string name, string value)>();
        
        foreach (var (name, value) in element.Attributes)
        {
            if (IsCodeBlock(value))
            {
                var codeContent = value.Trim().TrimStart('{').TrimEnd('}').Trim();
                // Use ResolveCodeExpressionForPropAssignment to get the raw expression without .ToString()
                // Then we'll add .ToString() ?? "" when generating the temporary variable
                var rawExpression = ResolveCodeExpressionForPropAssignment(codeContent, propsMap, globalPropsTypeName, varsMap, varsTypeMap);
                
                // Generate a unique variable name for this attribute
                var baseVarName = $"attr_{name.Replace("-", "_")}";
                var varName = GetUniqueVariableName(baseVarName, usedVariableNames);
                
                // ALWAYS use temporary variables for code blocks to avoid string interpolation issues
                // This is simpler and safer than trying to detect quotes
                codeBlockAttrs.Add((name, codeContent, rawExpression, varName));
            }
            else
            {
                // Static attribute value - escape for interpolation
                var escapedValue = EscapeAttributeValue(value);
                staticAttrs.Add((name, escapedValue));
            }
        }
        
        // Generate temporary variables for code blocks with string literals
        if (codeBlockAttrs.Any())
        {
            foreach (var (name, codeContent, rawExpression, varName) in codeBlockAttrs)
            {
                // Convert the expression to string with null coalescing
                // Handle boolean literals (false, true) - they're not nullable so use .ToString() directly
                // Handle expressions that already return strings (like props.AriaLabel ?? "Select site language")
                if (rawExpression.Trim() == "false" || rawExpression.Trim() == "true")
                {
                    sb.AppendLine($"{indent}var {varName} = {rawExpression}.ToString();");
                }
                else
                {
                    sb.AppendLine($"{indent}var {varName} = ({rawExpression})?.ToString() ?? \"\";");
                }
            }
        }
        
        // Start building the tag with string interpolation
        sb.Append($"{indent}writer.Write($\"<{element.TagName}");
        
        // Add static attributes and code block attributes
        foreach (var (name, value) in staticAttrs)
        {
            sb.Append($" {name}=\\\"{value}\\\"");
        }
        
        // Add code block attributes that use temporary variables
        foreach (var (name, _, _, varName) in codeBlockAttrs)
        {
            sb.Append($" {name}=\\\"{{{varName}}}\\\"");
        }

        if (element.IsSelfClosing)
        {
            sb.Append(" /");
        }

        sb.AppendLine(">\\n\");");
    }
    
    /// <summary>
    /// Generates a unique variable name by appending a counter if the name is already used.
    /// </summary>
    private static string GetUniqueVariableName(string baseName, HashSet<string> usedVariableNames)
    {
        if (usedVariableNames == null)
        {
            usedVariableNames = new HashSet<string>();
        }
        
        var varName = baseName;
        var counter = 0;
        
        // Find the first available name
        while (usedVariableNames.Contains(varName))
        {
            counter++;
            varName = $"{baseName}_{counter}";
        }
        
        usedVariableNames.Add(varName);
        return varName;
    }
    
    /// <summary>
    /// Checks if an attribute value is a code block (starts with { and ends with }).
    /// </summary>
    private static bool IsCodeBlock(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
            
        var trimmed = value.Trim();
        return trimmed.StartsWith("{") && trimmed.EndsWith("}");
    }
    
    /// <summary>
    /// Checks if a CodeNode represents a RenderPipe prop that can be passed directly.
    /// </summary>
    private static bool IsRenderPipeProp(CodeNode codeNode, Dictionary<string, ComponentPropInfo>? propsMap)
    {
        if (propsMap == null)
            return false;
            
        var content = codeNode.Content.Trim();
        
        // Must be a props access: props.X
        if (!content.StartsWith("props.", StringComparison.OrdinalIgnoreCase))
            return false;
            
        var propName = content.Substring(6).Trim();
        
        // Check if this prop is a RenderPipe (with or without generic parameter)
        if (propsMap.TryGetValue(propName, out var propInfo))
        {
            var typeName = propInfo.TypeName?.Trim();
            return !string.IsNullOrEmpty(typeName) && (
                typeName.Equals("Shared.Meta.RenderPipe", StringComparison.OrdinalIgnoreCase) ||
                typeName.Equals("RenderPipe", StringComparison.OrdinalIgnoreCase) ||
                typeName.StartsWith("RenderPipe<", StringComparison.OrdinalIgnoreCase));
        }
        
        return false;
    }
    
    /// <summary>
    /// Builds the opening tag string with attributes (legacy method for simple cases).
    /// </summary>
    private static string BuildOpeningTag(ElementNode element)
    {
        var sb = new StringBuilder();
        sb.Append($"<{element.TagName}");

        foreach (var (name, value) in element.Attributes)
        {
            if (IsCodeBlock(value))
            {
                // For code blocks, we'll need to handle this differently
                // This method is only used for simple static tags now
                sb.Append($" {name}=\"{{code}}\"");
            }
            else
            {
                sb.Append($" {name}=\"{EscapeAttributeValue(value)}\"");
            }
        }

        if (element.IsSelfClosing)
        {
            sb.Append(" /");
        }

        sb.Append(">");
        return sb.ToString();
    }

    /// <summary>
    /// Generates rendering code for text content.
    /// </summary>
    private static void GenerateTextRendering(StringBuilder sb, TextNode text, int indentLevel, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null)
    {
        var indent = new string(' ', indentLevel * 4);
        var content = text.Content;
        
        // Check if content contains code blocks (e.g., {lang} inside a for loop)
        // If so, we need to use string interpolation instead of verbatim strings
        if (content.Contains('{') && content.Contains('}'))
        {
            // Try to detect code blocks in the text
            // Simple heuristic: look for { followed by alphanumeric characters and }
            var codeBlockPattern = @"\{([a-zA-Z_][a-zA-Z0-9_.]*)\}";
            var matches = System.Text.RegularExpressions.Regex.Matches(content, codeBlockPattern);
            
            if (matches.Count > 0)
            {
                // Found code blocks - use string interpolation
                var interpolatedContent = content;
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var codeExpression = match.Groups[1].Value; // Extract the code expression (e.g., "lang")
                    // Use ResolveCodeExpressionForPropAssignment to get the raw expression (e.g., "lang")
                    // This returns just the variable name, not writer.Write(...)
                    var resolvedCode = ResolveCodeExpressionForPropAssignment(codeExpression, propsMap, globalPropsTypeName, varsMap, varsTypeMap);
                    // Replace {codeExpression} with {resolvedCode} for string interpolation
                    // Note: In C# interpolated strings, we use {variable}, not ${variable}
                    interpolatedContent = interpolatedContent.Replace(match.Value, $"{{{resolvedCode}}}");
                }
                
                // Escape the content for string interpolation (escape quotes and backslashes)
                var escapedContent = EscapeStringForRegularLiteral(interpolatedContent);
                sb.AppendLine($"{indent}writer.Write($\"{escapedContent}\");");
                return;
            }
        }
        
        // No code blocks found - use regular text rendering
        // Check if content contains non-ASCII characters
        bool hasUnicode = content.Any(c => c > 127);
        
        if (hasUnicode)
        {
            // Use regular string literal with Unicode escapes for non-ASCII characters
            var escapedContent = EscapeStringForRegularLiteral(content);
            sb.AppendLine($"{indent}writer.Write(\"{escapedContent}\\n\");");
        }
        else
        {
            // Use verbatim string literal for ASCII-only content
            var escapedContent = EscapeString(content);
            sb.AppendLine($"{indent}writer.Write(@\"{escapedContent}\n\");");
        }
    }

    /// <summary>
    /// Generates rendering code for a comment.
    /// </summary>
    private static void GenerateCommentRendering(StringBuilder sb, CommentNode comment, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var content = EscapeString(comment.Content);
        sb.AppendLine($"{indent}writer.Write(@\"<!--{content}-->\n\");");
    }

    /// <summary>
    /// Generates rendering code for a code block.
    /// Context-aware: generates appropriate code based on where the code block appears.
    /// </summary>
    /// <param name="sb">StringBuilder to write generated code to</param>
    /// <param name="code">The code node to render</param>
    /// <param name="indentLevel">Indentation level</param>
    /// <param name="allScripts">List of all scripts for RenderScripts() handling</param>
    /// <param name="propsMap">Map of prop names to prop info (for type checking)</param>
    /// <param name="context">The context where this code block appears (TextContent, AttributeValue, PropAssignment)</param>
    private static void GenerateCodeRendering(StringBuilder sb, CodeNode code, int indentLevel, List<(string id, string content)> allScripts, List<(string id, string content)> allStyles, Dictionary<string, ComponentPropInfo>? propsMap, CodeBlockContext context, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null)
    {
        var indent = new string(' ', indentLevel * 4);
        var content = code.Content.Trim();
        
        // Handle special code blocks
        if (content.Equals("RenderChildren()", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"{indent}await renderContext.RenderAsync(children);");
            return;
        }
        
        if (content.Equals("RenderScripts()", StringComparison.OrdinalIgnoreCase))
        {
            // Render script tags for all collected scripts
            if (allScripts != null && allScripts.Any())
            {
                foreach (var (scriptId, _) in allScripts)
                {
                    sb.AppendLine($"{indent}writer.Write($\"\\n<script src=\\\"/script/{scriptId}\\\" defer></script>\\n\");");
                }
            }
            return;
        }
        
        if (content.Equals("RenderStyles()", StringComparison.OrdinalIgnoreCase))
        {
            // Render style link tags for all collected styles
            if (allStyles != null && allStyles.Any())
            {
                foreach (var (styleId, _) in allStyles)
                {
                    sb.AppendLine($"{indent}writer.Write($\"\\n<link rel=\\\"stylesheet\\\" href=\\\"/style/{styleId}\\\" />\\n\");");
                }
            }
            return;
        }
        
        // Resolve the code expression to C# code (with prop type awareness)
        var resolvedCode = ResolveCodeExpression(content, propsMap, context, globalPropsTypeName, globalPropsInfo, varsMap, varsTypeMap);
        
        // Check if this is an IEnumerable render marker
        if (resolvedCode.StartsWith("__IENUMERABLE_RENDER__") && resolvedCode.EndsWith("__"))
        {
            // Extract var name and component type from marker
            var markerContent = resolvedCode.Substring("__IENUMERABLE_RENDER__".Length).TrimEnd('_');
            var parts = markerContent.Split(new[] { "__" }, StringSplitOptions.None);
            if (parts.Length == 2 && context == CodeBlockContext.TextContent)
            {
                var varName = parts[0];
                var componentType = parts[1];
                
                // Generate properly indented foreach loop
                sb.AppendLine($"{indent}foreach (var item in {varName})");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    await {componentType}.RenderAsync(renderContext, item);");
                sb.AppendLine($"{indent}}}");
                return;
            }
        }
        
        // Generate code based on context
        switch (context)
        {
            case CodeBlockContext.TextContent:
                // In text content: write the expression result to writer
                // For RenderPipe props, render directly; for others, use ToString
                sb.AppendLine($"{indent}{resolvedCode}");
                break;
                
            case CodeBlockContext.AttributeValue:
                // In attribute value: use string interpolation
                // Example: <div title={props.Title}> -> writer.Write($" title=\"{props.Title}\"");
                // Note: This is handled differently - we'll generate the full attribute with interpolation
                // For now, just return the resolved expression for use in attribute generation
                sb.AppendLine($"{indent}writer.Write($\"\\\"{resolvedCode}\\\"\");");
                break;
                
            case CodeBlockContext.PropAssignment:
                // In prop assignment: use expression directly
                // Example: Content={props.Content} -> Content = props.Content
                // This is handled by the caller who generates the prop assignment
                sb.AppendLine($"{indent}{resolvedCode}");
                break;
        }
    }
    
    /// <summary>
    /// Checks if an expression represents a RenderPipe type.
    /// Checks propsMap for known prop types, or uses heuristics for generic expressions.
    /// </summary>
    private static bool IsRenderPipeExpression(string expression, Dictionary<string, ComponentPropInfo>? propsMap = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null)
    {
        expression = expression.Trim();
        
        // Check props.X expressions
        if (expression.StartsWith("props.", StringComparison.OrdinalIgnoreCase))
        {
            var propName = expression.Substring(6).Trim();
            if (propsMap != null && propsMap.TryGetValue(propName, out var propInfo))
            {
                var typeName = propInfo.TypeName?.Trim();
                return !string.IsNullOrEmpty(typeName) && (
                    typeName.Equals("Shared.Meta.RenderPipe", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Equals("RenderPipe", StringComparison.OrdinalIgnoreCase) ||
                    typeName.StartsWith("RenderPipe<", StringComparison.OrdinalIgnoreCase));
            }
        }
        
        // Check vars.X expressions
        if (expression.StartsWith("vars.", StringComparison.OrdinalIgnoreCase))
        {
            var varName = expression.Substring(5).Trim();
            // Check if there's property access (e.g., vars.Content.Body)
            var dotIndex = varName.IndexOf('.');
            if (dotIndex > 0)
            {
                var actualVarName = varName.Substring(0, dotIndex).Trim();
                var propertyName = varName.Substring(dotIndex + 1).Trim();
                
                if (varsMap != null && varsMap.Contains(actualVarName))
                {
                    if (varsTypeMap != null && varsTypeMap.TryGetValue(actualVarName, out var varType) && varType != null)
                    {
                        // Check if the property is "Body" and the var type is a props type
                        // For now, assume Body is always a RenderPipe when accessing vars.X.Body
                        if (propertyName.Equals("Body", StringComparison.OrdinalIgnoreCase))
                        {
                            return true; // Body is assumed to be RenderPipe
                        }
                    }
                }
            }
            else
            {
                // No property access - check if var itself is RenderPipe
                if (varsMap != null && varsMap.Contains(varName))
                {
                    if (varsTypeMap != null && varsTypeMap.TryGetValue(varName, out var varType) && varType != null)
                    {
                        return varType.Contains("RenderPipe", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
        
        // Check bare var name (without vars. prefix)
        if (varsMap != null && varsMap.Contains(expression))
        {
            if (varsTypeMap != null && varsTypeMap.TryGetValue(expression, out var varType) && varType != null)
            {
                return varType.Contains("RenderPipe", StringComparison.OrdinalIgnoreCase);
            }
        }
        
        // Check globalProps.X expressions (could theoretically be RenderPipes in the future)
        // For now, globalProps are assumed to be simple types, but we check propsMap if available
        if (expression.StartsWith("globalProps.", StringComparison.OrdinalIgnoreCase))
        {
            // Currently globalProps are simple types, but we could add type checking here if needed
            return false;
        }
        
        // For generic expressions, check if it looks like a RenderPipe call
        // Heuristic: if expression ends with "(renderContext)" or similar, it might be a RenderPipe
        // But we'll be conservative and only treat known patterns as RenderPipes
        return false;
    }
    
    /// <summary>
    /// Resolves a code expression to C# code.
    /// Handles props access, globalProps access, vars access, and generic expressions.
    /// Automatically detects RenderPipes vs string-able types and generates appropriate code.
    /// </summary>
    /// <param name="content">The code expression content</param>
    /// <param name="propsMap">Map of prop names to prop info (for type checking)</param>
    /// <param name="context">The context where this expression appears</param>
    private static string ResolveCodeExpression(string content, Dictionary<string, ComponentPropInfo>? propsMap = null, CodeBlockContext context = CodeBlockContext.TextContent, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null)
    {
        content = content.Trim();
        
        // Check if this is a RenderPipe expression
        var isRenderPipe = IsRenderPipeExpression(content, propsMap, varsMap, varsTypeMap);
        
        // Handle RenderPipe expressions
        if (isRenderPipe)
        {
            // Resolve vars.X to X before wrapping in await call
            string resolvedContent = content;
            if (content.StartsWith("vars.", StringComparison.OrdinalIgnoreCase))
            {
                var varName = content.Substring(5).Trim();
                // Check if there's property access (e.g., vars.Content.Body)
                var dotIndex = varName.IndexOf('.');
                string propertyAccess = "";
                if (dotIndex > 0)
                {
                    // Extract property access part (e.g., ".Body")
                    propertyAccess = varName.Substring(dotIndex);
                    // Extract just the var name (e.g., "Content")
                    varName = varName.Substring(0, dotIndex).Trim();
                }
                
                if (varsMap != null && varsMap.Contains(varName))
                {
                    resolvedContent = varName + propertyAccess; // Use local variable name with property access
                }
            }
            
            if (context == CodeBlockContext.TextContent)
            {
                // In text content: call the RenderPipe with renderContext
                return $"await {resolvedContent}(renderContext);";
            }
            else if (context == CodeBlockContext.PropAssignment)
            {
                // In prop assignment: pass as-is (no await)
                return resolvedContent;
            }
            else
            {
                // In attribute value: not supported (RenderPipe can't be used in attributes)
                return $"/* ERROR: RenderPipe expressions cannot be used in attribute values */ null";
            }
        }
        
        // Handle non-RenderPipe expressions (string-able types)
        // Extract the base expression (props.X, globalProps.X, vars.X, or generic)
        string baseExpression;
        bool isLiteral = false;
        bool isAlreadyString = false; // True if baseExpression already returns a string (e.g., globalProps?.X ?? "")
        bool isNullable = false; // True if the property is nullable
        
        if (content.StartsWith("props.", StringComparison.OrdinalIgnoreCase))
        {
            baseExpression = content; // Use as-is
            
            // Check if this property is nullable from propsMap
            var propName = content.Substring(6).Trim(); // Remove "props." prefix
            // Check if the expression contains ?? operator with string literal
            if (content.Contains("??") && (content.Contains("\"") || content.Contains("'")))
            {
                // Expression contains string literal - will need special handling
                // Set baseExpression to the full expression including quotes
                baseExpression = content;
                isNullable = true; // Treat as nullable since it has ?? operator
            }
            else if (propsMap != null)
            {
                var propInfo = propsMap.Values.FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
                if (propInfo != null)
                {
                    isNullable = propInfo.IsNullable;
                    
                    // If the property is already a string type and non-nullable, it's already a string
                    if (!isNullable && propInfo.TypeName.Equals("System.String", StringComparison.OrdinalIgnoreCase))
                    {
                        isAlreadyString = true;
                    }
                }
            }
        }
        else if (content.StartsWith("globalProps.", StringComparison.OrdinalIgnoreCase))
        {
            // GlobalProps should always be provided - if it's null, that's a programming error
            // Access properties directly without null-safe operators
            var propName = content.Substring(12).Trim();
            
            // Check if this property is nullable from globalPropsInfo
            if (globalPropsInfo != null)
            {
                var propInfo = globalPropsInfo.FirstOrDefault(p => p.name.Equals(propName, StringComparison.OrdinalIgnoreCase));
                isNullable = propInfo.isNullable;
            }
            
            baseExpression = $"globalProps.{propName}";
            
            // If the property is already a string type and non-nullable, it's already a string
            if (!isNullable && globalPropsInfo != null)
            {
                var propInfo = globalPropsInfo.FirstOrDefault(p => p.name.Equals(propName, StringComparison.OrdinalIgnoreCase));
                if (propInfo.type.Equals("System.String", StringComparison.OrdinalIgnoreCase))
                {
                    isAlreadyString = true;
                }
            }
        }
        else if (content.StartsWith("vars.", StringComparison.OrdinalIgnoreCase))
        {
            var varName = content.Substring(5).Trim();
            // Check if there's a property access after the var name (e.g., vars.Content.Title)
            var dotIndex = varName.IndexOf('.');
            string propertyAccess = "";
            if (dotIndex > 0)
            {
                // Extract property access part (e.g., ".Title")
                propertyAccess = varName.Substring(dotIndex);
                // Extract just the var name (e.g., "Content")
                varName = varName.Substring(0, dotIndex).Trim();
            }
            
            // Vars are local variables, so resolve vars.X to just X
            if (varsMap != null && varsMap.Contains(varName))
            {
                // Check if this var is a RenderPipe type
                if (varsTypeMap != null && varsTypeMap.TryGetValue(varName, out var varType) && varType != null)
                {
                    // Check if it's a RenderPipe type
                    if (varType.Contains("RenderPipe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (context == CodeBlockContext.TextContent)
                        {
                            // In text content: call the RenderPipe with renderContext
                            // If there's property access, that's an error (can't access properties on RenderPipe)
                            if (!string.IsNullOrEmpty(propertyAccess))
                            {
                                return $"/* ERROR: Cannot access properties on RenderPipe: vars.{varName}{propertyAccess} */ null";
                            }
                            return $"await {varName}(renderContext);";
                        }
                        else if (context == CodeBlockContext.PropAssignment)
                        {
                            // In prop assignment: pass as-is (no await)
                            if (!string.IsNullOrEmpty(propertyAccess))
                            {
                                return $"/* ERROR: Cannot access properties on RenderPipe: vars.{varName}{propertyAccess} */ null";
                            }
                            baseExpression = varName;
                            isAlreadyString = true; // RenderPipe is not a string
                        }
                        else
                        {
                            // In attribute value: not supported
                            return $"/* ERROR: RenderPipe expressions cannot be used in attribute values */ null";
                        }
                    }
                    // Check if this var is an IEnumerable
                    else if (varType.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract the generic type parameter
                        var genericStart = varType.IndexOf('<');
                        var genericEnd = varType.LastIndexOf('>');
                        if (genericStart != -1 && genericEnd != -1)
                        {
                            var propsTypeName = varType.Substring(genericStart + 1, genericEnd - genericStart - 1).Trim();

                            // Infer component type from props type
                            var componentType = InferComponentTypeFromProps(propsTypeName);

                            if (componentType != null && context == CodeBlockContext.TextContent)
                            {
                                // Generate code to iterate and render each component
                                // Return a special marker that GenerateCodeRendering will handle
                                // We'll use a special format that GenerateCodeRendering can detect
                                return $"__IENUMERABLE_RENDER__{varName}__{componentType}__";
                            }
                        }
                    }
                }
                
                // Not a RenderPipe or IEnumerable - use the local variable name with property access
                baseExpression = varName + propertyAccess;
            }
            else
            {
                baseExpression = $"vars.{varName}{propertyAccess}"; // Fallback if var not found
            }
            isAlreadyString = true; // Vars are typically strings or RenderPipes
        }
        else
        {
            // Check if this is a literal value (boolean, number, string, etc.)
            var trimmedContent = content.Trim();
            
            // Check if this is a var name without "vars." prefix
            if (varsMap != null && varsMap.Contains(trimmedContent))
            {
                // Check if this var is a RenderPipe type
                if (varsTypeMap != null && varsTypeMap.TryGetValue(trimmedContent, out var varType) && varType != null)
                {
                    // Check if it's a RenderPipe type
                    if (varType.Contains("RenderPipe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (context == CodeBlockContext.TextContent)
                        {
                            // In text content: call the RenderPipe with renderContext
                            return $"await {trimmedContent}(renderContext);";
                        }
                        else if (context == CodeBlockContext.PropAssignment)
                        {
                            // In prop assignment: pass as-is (no await)
                            return trimmedContent;
                        }
                        else
                        {
                            // In attribute value: not supported
                            return $"/* ERROR: RenderPipe expressions cannot be used in attribute values */ null";
                        }
                    }
                }
                
                // Not a RenderPipe, treat as regular var
                baseExpression = trimmedContent;
                isAlreadyString = true;
            }
            else if (trimmedContent.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                trimmedContent.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                trimmedContent.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                isLiteral = true;
                baseExpression = trimmedContent;
            }
            else if (double.TryParse(trimmedContent, out _) || int.TryParse(trimmedContent, out _))
            {
                isLiteral = true;
                baseExpression = trimmedContent;
            }
            else if ((trimmedContent.StartsWith("\"") && trimmedContent.EndsWith("\"")) ||
                     (trimmedContent.StartsWith("'") && trimmedContent.EndsWith("'")) ||
                     (trimmedContent.StartsWith("@\"") && trimmedContent.EndsWith("\"")))
            {
                isLiteral = true;
                isAlreadyString = true; // String literals are already strings
                baseExpression = trimmedContent;
            }
            else
            {
                // Generic expression: use as-is
                baseExpression = content;
            }
        }
        
        // Ensure baseExpression is set
        if (baseExpression == null)
        {
            baseExpression = content;
        }
        
        // Generate appropriate code based on context
        switch (context)
        {
            case CodeBlockContext.TextContent:
                // In text content: write the expression result to writer
                if (isAlreadyString)
                {
                    // Already a string, use directly
                    return $"writer.Write({baseExpression});";
                }
                else if (isLiteral)
                {
                    // Literal value, use .ToString() directly
                    return $"writer.Write({baseExpression}.ToString());";
                }
                else if (!isNullable)
                {
                    // Non-nullable property, use .ToString() directly (no null check needed)
                    return $"writer.Write({baseExpression}.ToString());";
                }
                else
                {
                    // Nullable expressions, use ?.ToString() for null safety
                    return $"writer.Write({baseExpression}?.ToString() ?? \"\");";
                }
                
            case CodeBlockContext.AttributeValue:
                // In attribute value: return expression for string interpolation
                // IMPORTANT: If the expression contains string literals with quotes, we need to handle them carefully
                // to avoid breaking the outer string interpolation
                if (isAlreadyString)
                {
                    // Already a string, use directly
                    return baseExpression;
                }
                else if (isLiteral)
                {
                    // Literal value, use .ToString() directly
                    return $"{baseExpression}.ToString()";
                }
                else if (!isNullable)
                {
                    // Non-nullable property, use .ToString() directly (no null check needed)
                    return $"{baseExpression}.ToString()";
                }
                else
                {
                    // Nullable expressions, use ?.ToString() for null safety
                    // Check if baseExpression contains string literals (quotes) - if so, wrap in parentheses
                    if (baseExpression.Contains("\"") || baseExpression.Contains("'"))
                    {
                        // Expression contains string literals - wrap in parentheses to ensure proper evaluation
                        return $"({baseExpression})?.ToString() ?? \"\"";
                    }
                    return $"{baseExpression}?.ToString() ?? \"\"";
                }
                
            case CodeBlockContext.PropAssignment:
                // In prop assignment: return expression as-is
                return baseExpression;
                
            default:
                return baseExpression;
        }
    }
    
    /// <summary>
    /// Resolves a code expression for prop assignment.
    /// Returns the raw expression without ToString conversion (for prop assignments).
    /// </summary>
    private static string ResolveCodeExpressionForPropAssignment(string content, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null)
    {
        content = content.Trim();
        
        // Props access: {props.Title} -> props.Title (no ToString conversion for prop assignment)
        if (content.StartsWith("props.", StringComparison.OrdinalIgnoreCase))
        {
            var propName = content.Substring(6).Trim();
            // RenderPipe props are passed as-is
            return $"props.{propName}";
        }
        
        // GlobalProps access: {globalProps.Language} -> globalProps.Language (no ToString conversion for prop assignment)
        if (content.StartsWith("globalProps.", StringComparison.OrdinalIgnoreCase))
        {
            var propName = content.Substring(12).Trim();
            return $"globalProps.{propName}";
        }
        
        // Vars access: {vars.X} -> X (local variable name), {vars.X.Y} -> X.Y
        if (content.StartsWith("vars.", StringComparison.OrdinalIgnoreCase))
        {
            var varName = content.Substring(5).Trim();
            // Check if there's property access (e.g., vars.Content.Title)
            var dotIndex = varName.IndexOf('.');
            string propertyAccess = "";
            if (dotIndex > 0)
            {
                // Extract property access part (e.g., ".Title")
                propertyAccess = varName.Substring(dotIndex);
                // Extract just the var name (e.g., "Content")
                varName = varName.Substring(0, dotIndex).Trim();
            }
            
            if (varsMap != null && varsMap.Contains(varName))
            {
                // Use the local variable name with property access
                return varName + propertyAccess;
            }
            else
            {
                return $"vars.{varName}{propertyAccess}"; // Fallback if var not found
            }
        }
        
        // Generic expression: lift code directly
        // Examples: {someMethod()} -> someMethod()
        //           {obj.Property} -> obj.Property
        //           {"Hello" + props.Name} -> "Hello" + props.Name
        return content;
    }
    
    /// <summary>
    /// Context where a code block can appear.
    /// </summary>
    private enum CodeBlockContext
    {
        /// <summary>
        /// Code block appears in text content (between tags)
        /// Example: Hello {props.Name}!
        /// </summary>
        TextContent,
        
        /// <summary>
        /// Code block appears in an attribute value
        /// Example: &lt;div title={props.Title}&gt;
        /// </summary>
        AttributeValue,
        
        /// <summary>
        /// Code block appears in a prop assignment (component attribute)
        /// Example: &lt;:Component Content={props.Content} /&gt;
        /// </summary>
        PropAssignment
    }

    /// <summary>
    /// Generates rendering code for a component node.
    /// Components are rendered via static methods on the component class.
    /// </summary>
    private static void GenerateComponentRendering(StringBuilder sb, ComponentNode component, int indentLevel, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, string? relativePath = null, Dictionary<string, string>? globalComponents = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null, string? templatesRoot = null)
    {
        var indent = new string(' ', indentLevel * 4);
        var rawComponentName = component.ComponentName;
        var hasChildren = component.Children.Count > 0;
        var hasAttributes = component.Attributes.Count > 0;
        
        // Resolve component name based on syntax:
        // 1. Global component: just use the name (e.g., "Container")
        // 2. Relative component: ".ComponentName" or "./ComponentName" (same directory)
        // 3. Fully qualified: "Namespace.ComponentName"
        string resolvedComponentName;
        string fullComponentPath;
        
        if (rawComponentName.StartsWith("."))
        {
            // Relative component: .ComponentName or ./ComponentName
            // Remove leading . and / characters (do separately to handle both cases)
            var relativeName = rawComponentName.TrimStart('.').TrimStart('/');
            if (string.IsNullOrEmpty(relativeName))
            {
                // Invalid: just "." or "./" - use empty string and let it error later
                resolvedComponentName = "";
                fullComponentPath = $"Templates.Generated.Components.";
            }
            else
            {
                resolvedComponentName = ToPascalCase(relativeName);
                
                // For relative imports, use the current component's namespace
                // Extract namespace from relativePath (e.g., components/header/index.chtml -> Templates.Generated.Components.Header)
                if (!string.IsNullOrEmpty(relativePath))
                {
                    var segments = relativePath.Split('/');
                    var componentsIndex = -1;
                    
                    for (int i = 0; i < segments.Length; i++)
                    {
                        if (segments[i].Equals("components", StringComparison.OrdinalIgnoreCase))
                        {
                            componentsIndex = i;
                            break;
                        }
                    }
                    
                    if (componentsIndex >= 0)
                    {
                        var nsParts = new List<string> { "Templates", "Generated", "Components" };
                        
                        // Add folder names after "components" up to (but not including) the file name
                        for (int i = componentsIndex + 1; i < segments.Length - 1; i++)
                        {
                            var segment = segments[i];
                            segment = RemoveBracketNotationForComponent(segment);
                            var cleaned = System.Text.RegularExpressions.Regex.Replace(segment, "[^A-Za-z0-9]", "_");
                            if (!string.IsNullOrEmpty(cleaned))
                            {
                                nsParts.Add(StringUtils.ToPascalCase(cleaned));
                            }
                        }
                        
                        var currentNamespace = string.Join(".", nsParts);
                        fullComponentPath = $"{currentNamespace}.{resolvedComponentName}";
                    }
                    else
                    {
                        // Fallback if we can't determine namespace
                        fullComponentPath = $"Templates.Generated.Components.{resolvedComponentName}";
                    }
                }
                else
                {
                    // Fallback if no relativePath
                    fullComponentPath = $"Templates.Generated.Components.{resolvedComponentName}";
                }
            }
        }
        else if (rawComponentName.Contains("."))
        {
            // Fully qualified: Namespace.ComponentName
            var parts = rawComponentName.Split('.');
            var namespacePart = parts[0];
            var componentPart = parts[parts.Length - 1];
            resolvedComponentName = ToPascalCase(componentPart);
            fullComponentPath = $"Templates.Generated.Components.{namespacePart}.{resolvedComponentName}";
        }
        else
        {
            // Check if it's a global component
            if (globalComponents != null && globalComponents.TryGetValue(rawComponentName, out var globalComponentPath))
            {
                resolvedComponentName = rawComponentName;
                fullComponentPath = globalComponentPath;
            }
            else
            {
                // Default: assume it's in Components namespace
                resolvedComponentName = ToPascalCase(rawComponentName);
                fullComponentPath = $"Templates.Generated.Components.{resolvedComponentName}";
            }
        }
        
        var componentName = resolvedComponentName;

        // Ensure fullComponentPath includes the class name for nested namespaces
        // Components in nested namespaces (parts.Length > 3) need the class name appended
        var pathParts = fullComponentPath.Split('.');
        if (pathParts.Length > 3 && !fullComponentPath.EndsWith(".Index") && !fullComponentPath.EndsWith(".Stub"))
        {
            // Append class name
            // For relative imports, resolvedComponentName is already the class name (e.g., "Stub")
            // For other components, assume "Index" (most common case for index files)
            if (rawComponentName.StartsWith(".") && !string.IsNullOrEmpty(resolvedComponentName))
            {
                // Relative import - use the resolved component name as class name
                fullComponentPath = $"{fullComponentPath}.{resolvedComponentName}";
            }
            else
            {
                // Default to "Index" for nested namespace components
                fullComponentPath = $"{fullComponentPath}.Index";
            }
        }
        
        // Recalculate pathParts after updating fullComponentPath
        pathParts = fullComponentPath.Split('.');

        // Build component call - components are rendered via static methods
        // Format: await {ComponentName}.RenderAsync(renderContext, props, children...)
        sb.AppendLine($"{indent}await {fullComponentPath}.RenderAsync(");
        sb.AppendLine($"{indent}    renderContext,");
        
        // Generate props instance from attributes
        // Components are called with InputProps (what RenderAsync accepts)
        // - If component has computed props: use InputProps (e.g., IndexInputProps)
        // - If component has no computed props: use Props (which is the same as InputProps in that case)
        // The component's RenderAsync accepts TInputProps and transforms internally if needed
        if (hasAttributes)
        {
            // Determine which props type to use:
            // - Components with computed props: RenderAsync accepts InputProps (e.g., IndexInputProps)
            // - Components without computed props: RenderAsync accepts Props (e.g., IndexProps)
            // Since we can't determine this at code generation time, we'll use Props as default
            // Components with computed props will need InputProps, which we'll handle via a naming pattern
            // Default to Props - if InputProps exists, the component will have computed props and we can use that
            var componentBaseName = pathParts[pathParts.Length - 1]; // e.g., "Index", "Experience"
            
            var propsTypeName = pathParts.Length > 3 
                ? $"{string.Join(".", pathParts.Take(pathParts.Length - 1))}.{pathParts[pathParts.Length - 1]}Props"
                : $"{fullComponentPath}.{componentName}Props";
            var inputPropsTypeName = pathParts.Length > 3 
                ? $"{string.Join(".", pathParts.Take(pathParts.Length - 1))}.{pathParts[pathParts.Length - 1]}InputProps"
                : $"{fullComponentPath}.{componentName}InputProps";
            
            // Default to Props - components without computed props use Props
            // Components with computed props will have both InputProps and Props, but RenderAsync accepts InputProps
            // Codeblock now processes at compile time, so it uses Props (not InputProps)
            var typeToUse = propsTypeName; // Use Props for all components now
            sb.AppendLine($"{indent}    new {typeToUse}");
            sb.AppendLine($"{indent}    {{");
            
            var attrList = component.Attributes.ToList();
            
            // Special handling for Codeblock component with rawUrl starting with ~/
            // Load file content at compile time and process with Shiki
            if (componentName.Equals("Codeblock", StringComparison.OrdinalIgnoreCase) &&
                component.Attributes.TryGetValue("rawUrl", out var rawUrlValue) &&
                !string.IsNullOrWhiteSpace(rawUrlValue) &&
                rawUrlValue.StartsWith("~/", StringComparison.Ordinal))
            {
                // Extract language from attributes
                string? language = null;
                if (component.Attributes.TryGetValue("lang", out var langValue))
                {
                    language = langValue;
                }
                language ??= "text";
                
                // Resolve ~/ path to wwwroot
                // ~/files/snippets/file.cs -> wwwroot/files/snippets/file.cs
                var wwwrootPath = rawUrlValue.Substring(2); // Remove ~/
                if (string.IsNullOrEmpty(templatesRoot))
                {
                    // templatesRoot not available - generate error
                    var errorMsg = EscapeString($"<!-- Error: templatesRoot not available for loading {rawUrlValue} -->");
                    sb.AppendLine($"{indent}        Content = @\"{errorMsg}\",");
                }
                else
                {
                    var wwwrootDir = Path.Combine(Path.GetDirectoryName(templatesRoot) ?? templatesRoot, "wwwroot");
                    var filePath = Path.Combine(wwwrootDir, wwwrootPath.Replace('/', Path.DirectorySeparatorChar));
                    
                    // Load file content at compile time
                    string? fileContent = null;
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            fileContent = File.ReadAllText(filePath);
                        }
                        catch (Exception ex)
                        {
                            // If file loading fails, generate error comment
                            var errorMsg = EscapeString($"<!-- Error loading file {rawUrlValue}: {ex.Message} -->");
                            sb.AppendLine($"{indent}        Content = @\"{errorMsg}\",");
                        }
                    }
                    else
                    {
                        // File not found - generate error comment
                        var errorMsg = EscapeString($"<!-- Error: File not found: {filePath} -->");
                        sb.AppendLine($"{indent}        Content = @\"{errorMsg}\",");
                    }
                    
                    // Process loaded content with Shiki
                    if (fileContent != null)
                    {
                        var highlightedHtml = ShikiProcessor.HighlightCode(fileContent, language);
                        
                        if (highlightedHtml != null)
                        {
                            // Shiki succeeded - pass highlighted HTML as Content prop
                            var escapedHtml = EscapeString(highlightedHtml);
                            sb.AppendLine($"{indent}        Content = @\"{escapedHtml}\",");
                        }
                        else
                        {
                            // Shiki failed - fallback to plain code, still pass as Content
                            var escapedCode = EscapeString(System.Net.WebUtility.HtmlEncode(fileContent));
                            var languageClassEscaped = EscapeString($"language-{language}");
                            var htmlContent = $"<pre><code class=\"{languageClassEscaped}\">{escapedCode}</code></pre>";
                            var escapedHtml = EscapeString(htmlContent);
                            sb.AppendLine($"{indent}        Content = @\"{escapedHtml}\",");
                        }
                    }
                }
                
                // Convert ~/ to / for static file URL (e.g., ~/files/snippets/file.cs -> /files/snippets/file.cs)
                // This allows the download button to work with static file resolver
                var staticFileUrl = "/" + wwwrootPath;
                sb.AppendLine($"{indent}        RawUrl = @\"{EscapeString(staticFileUrl)}\",");
                
                // Add other attributes (like Filename) but skip rawUrl since we already added it above
                var otherAttrs = attrList.Where(a => 
                    !a.Key.Equals("rawUrl", StringComparison.OrdinalIgnoreCase) &&
                    !a.Key.Equals("Content", StringComparison.OrdinalIgnoreCase)).ToList();
                
                for (int i = 0; i < otherAttrs.Count; i++)
                {
                    var attr = otherAttrs[i];
                    var propName = ToPascalCase(attr.Key);
                    var isLast = i == otherAttrs.Count - 1;
                    var value = EscapeString(attr.Value);
                    sb.AppendLine($"{indent}        {propName} = @\"{value}\"{(isLast ? "" : ",")}");
                }
            }
            else
            {
                // Normal processing for other cases
                for (int i = 0; i < attrList.Count; i++)
                {
                    var attr = attrList[i];
                    var propName = ToPascalCase(attr.Key);
                    var isLast = i == attrList.Count - 1;
                    
                    // Special handling for Codeblock component - process Content with Shiki at compile time
                    if (componentName.Equals("Codeblock", StringComparison.OrdinalIgnoreCase) && attr.Key.Equals("Content", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract language from attributes
                        string? language = null;
                        if (component.Attributes.TryGetValue("lang", out var langValue))
                        {
                            language = langValue;
                        }
                        language ??= "text";
                        
                        // Get the content value (unescape HTML entities)
                        var contentValue = attr.Value
                            .Replace("&quot;", "\"")
                            .Replace("&#39;", "'")
                            .Replace("&lt;", "<")
                            .Replace("&gt;", ">")
                            .Replace("&amp;", "&");
                        
                        // Strip triple backticks if present
                        var code = contentValue;
                        code = System.Text.RegularExpressions.Regex.Replace(
                            code,
                            @"^```\w*\s*\r?\n?",
                            "",
                            System.Text.RegularExpressions.RegexOptions.Multiline
                        );
                        code = System.Text.RegularExpressions.Regex.Replace(
                            code,
                            @"\r?\n?```\s*$",
                            "",
                            System.Text.RegularExpressions.RegexOptions.Multiline
                        );
                        code = code.Trim();
                        
                        // Process with Shiki during compilation
                        var highlightedHtml = ShikiProcessor.HighlightCode(code, language);
                        
                        if (highlightedHtml != null)
                        {
                            // Shiki succeeded - pass highlighted HTML as Content prop directly
                            var escapedHtml = EscapeString(highlightedHtml);
                            sb.AppendLine($"{indent}        Content = @\"{escapedHtml}\"{(isLast ? "" : ",")}");
                        }
                        else
                        {
                            // Shiki failed - fallback to plain code, still pass as Content
                            var escapedCode = EscapeString(System.Net.WebUtility.HtmlEncode(code));
                            var languageClassEscaped = EscapeString($"language-{language}");
                            var htmlContent = $"<pre><code class=\"{languageClassEscaped}\">{escapedCode}</code></pre>";
                            var escapedHtml = EscapeString(htmlContent);
                            sb.AppendLine($"{indent}        Content = @\"{escapedHtml}\"{(isLast ? "" : ",")}");
                        }
                        continue; // Skip normal processing for Content attribute
                    }
                
                // Check if attribute value is a code block
                if (IsCodeBlock(attr.Value))
                {
                    // Extract code content: {props.X} -> props.X
                    var codeContent = attr.Value.Trim().TrimStart('{').TrimEnd('}').Trim();
                    // Resolve expression (for prop assignment, we want the raw expression, not ToString)
                    // Note: We don't have propsMap here - component attributes are passed from parent, not from current component's props
                    var resolvedCode = ResolveCodeExpressionForPropAssignment(codeContent, propsMap, globalPropsTypeName, varsMap, varsTypeMap);
                    sb.AppendLine($"{indent}        {propName} = {resolvedCode}{(isLast ? "" : ",")}");
                }
                else
                {
                    // Static string value
                    var value = EscapeString(attr.Value);
                    sb.AppendLine($"{indent}        {propName} = @\"{value}\"{(isLast ? "" : ",")}");
                }
            }
            
            sb.AppendLine($"{indent}    }}");
            }
        }
        else
        {
            // No attributes - use EmptyProps.Instance by default
            // Only create props instance for components that are known to have props
            // (e.g., LanguageSwitcher has AriaLabel prop)
            var componentPathLower = fullComponentPath.ToLowerInvariant();
            if (componentPathLower.Contains("languageswitcher"))
            {
                // LanguageSwitcher has props (AriaLabel) - create IndexProps instance
                var propsTypeName = pathParts.Length > 3 
                    ? $"{string.Join(".", pathParts.Take(pathParts.Length - 1))}.{pathParts[pathParts.Length - 1]}Props"
                    : $"{fullComponentPath}.{componentName}Props";
                sb.AppendLine($"{indent}    new {propsTypeName}()");
            }
            else
            {
                // Default: use EmptyProps.Instance for components without props
                sb.AppendLine($"{indent}    EmptyProps.Instance");
            }
        }

        if (hasChildren)
        {
            // Check if we can optimize: single child that is a RenderPipe prop
            CodeNode? codeNode = component.Children.Count == 1 && component.Children[0] is CodeNode cn ? cn : null;
            var canOptimize = codeNode != null && IsRenderPipeProp(codeNode, propsMap);
            
            if (canOptimize)
            {
                // Optimize: pass RenderPipe prop directly without wrapping
                var codeContent = codeNode!.Content.Trim();
                var propName = codeContent.Substring(6).Trim(); // Extract prop name from "props.X"
                sb.AppendLine($"{indent}    ,");
                sb.AppendLine($"{indent}    props.{propName}");
            }
            else
            {
                // Normal case: wrap children in RenderPipe
                sb.AppendLine($"{indent}    ,");
                sb.AppendLine($"{indent}    new RenderPipe<{globalPropsTypeName}>(async renderContext =>");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        var (writer, globalProps) = renderContext;");
                sb.AppendLine();
                
                foreach (var child in component.Children)
                {
                    GenerateNodeRendering(sb, child, indentLevel + 2, new List<(string id, string content)>(), new List<(string id, string content)>(), propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, new HashSet<string>());
                }
                
                sb.AppendLine($"{indent}    }})");
            }
        }
        
        sb.AppendLine($"{indent});");
    }

    /// <summary>
    /// Generates rendering code for DOCTYPE.
    /// </summary>
    private static void GenerateDoctypeRendering(StringBuilder sb, DocumentTypeNode doctype, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        // DOCTYPE content includes "doctype html" or similar - we need to extract just the declaration part
        var content = doctype.Content.Trim();
        // If content starts with "doctype", remove it since we'll add it ourselves
        if (content.StartsWith("doctype", StringComparison.OrdinalIgnoreCase))
        {
            content = content.Substring(7).TrimStart(); // Remove "doctype" prefix
        }
        // Ensure there's a space after DOCTYPE if content exists
        var escapedContent = EscapeString(content);
        var doctypeContent = string.IsNullOrEmpty(escapedContent) ? "" : $" {escapedContent}";
        sb.AppendLine($"{indent}writer.Write(@\"<!DOCTYPE{doctypeContent}>\n\");");
    }

    /// <summary>
    /// Generates rendering code for a style node.
    /// Bottom styles are not rendered inline - they're hoisted to RenderStyles().
    /// Inline styles are rendered as regular HTML.
    /// </summary>
    private static void GenerateStyleRendering(StringBuilder sb, StyleNode style, int indentLevel)
    {
        if (style.IsBottomStyle)
        {
            // Bottom styles are collected but not emitted inline - they're hoisted to RenderStyles()
            // Don't emit anything - style is collected and will be rendered at RenderStyles()
            return;
        }
        else
        {
            // Inline styles - render as regular HTML
            var indent = new string(' ', indentLevel * 4);
            sb.AppendLine($"{indent}writer.Write(@\"<style>\n\");");
            if (!string.IsNullOrWhiteSpace(style.Content))
            {
                var styleLines = style.Content.Split('\n');
                foreach (var line in styleLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"{indent}writer.Write(@\"{EscapeString(line)}\n\");");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}writer.Write(@\"\n\");");
                    }
                }
            }
            sb.AppendLine($"{indent}writer.Write(@\"</style>\n\");");
        }
    }

    /// <summary>
    /// Generates rendering code for a script node.
    /// Bottom scripts are not rendered inline - they're hoisted to RenderScripts().
    /// Inline scripts are rendered as regular HTML.
    /// </summary>
    private static void GenerateScriptRendering(StringBuilder sb, ScriptNode script, int indentLevel)
    {
        if (script.IsBottomScript)
        {
            // Bottom scripts are collected but not emitted inline - they're hoisted to RenderScripts()
            // Don't emit anything - script is collected and will be rendered at RenderScripts()
            return;
        }
        else
        {
            // Inline scripts - render as regular HTML
            var indent = new string(' ', indentLevel * 4);
            sb.AppendLine($"{indent}writer.Write(@\"<script>\n\");");
            if (!string.IsNullOrWhiteSpace(script.Content))
            {
                var scriptLines = script.Content.Split('\n');
                foreach (var line in scriptLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"{indent}writer.Write(@\"{EscapeString(line)}\n\");");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}writer.Write(@\"\n\");");
                    }
                }
            }
            sb.AppendLine($"{indent}writer.Write(@\"</script>\n\");");
        }
    }

    /// <summary>
    /// Generates rendering code for CDATA.
    /// </summary>
    private static void GenerateCDATARendering(StringBuilder sb, CDataNode cdata, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var content = EscapeString(cdata.Content);
        sb.AppendLine($"{indent}writer.Write(@\"<![CDATA[{content}]]>\n\");");
    }

    /// <summary>
    /// Generates rendering code for an if directive: {#if condition}...{/if}
    /// </summary>
    private static void GenerateIfRendering(StringBuilder sb, IfNode ifNode, int indentLevel, List<(string id, string content)> allScripts, List<(string id, string content)> allStyles, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, string? relativePath = null, Dictionary<string, string>? globalComponents = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null, HashSet<string>? usedVariableNames = null, string? templatesRoot = null)
    {
        var indent = new string(' ', indentLevel * 4);
        
        // Initialize variable name tracking if not provided
        if (usedVariableNames == null)
        {
            usedVariableNames = new HashSet<string>();
        }
        
        // Resolve the condition expression
        var condition = ResolveCodeExpression(ifNode.Condition, propsMap, CodeBlockContext.PropAssignment, globalPropsTypeName, globalPropsInfo, varsMap, varsTypeMap);
        
        // Generate if statement
        sb.AppendLine($"{indent}if ({condition})");
        sb.AppendLine($"{indent}{{");
        
        // Render children inside the if block
        foreach (var child in ifNode.Children)
        {
            GenerateNodeRendering(sb, child, indentLevel + 1, allScripts, allStyles, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, usedVariableNames, templatesRoot);
        }
        
        // Close if block
        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Generates rendering code for a for directive: {#for item in collection}...{/for}
    /// </summary>
    private static void GenerateForRendering(StringBuilder sb, ForNode forNode, int indentLevel, List<(string id, string content)> allScripts, List<(string id, string content)> allStyles, Dictionary<string, ComponentPropInfo>? propsMap = null, string globalPropsTypeName = "Shared.Generated.EmptyPropsInstance", List<(string name, string type, bool isNullable)>? globalPropsInfo = null, string? relativePath = null, Dictionary<string, string>? globalComponents = null, HashSet<string>? varsMap = null, Dictionary<string, string?>? varsTypeMap = null, HashSet<string>? usedVariableNames = null, string? templatesRoot = null)
    {
        var indent = new string(' ', indentLevel * 4);
        
        // Initialize variable name tracking if not provided
        if (usedVariableNames == null)
        {
            usedVariableNames = new HashSet<string>();
        }
        
        // Resolve the collection expression
        var collectionExpression = ResolveCodeExpression(forNode.CollectionExpression, propsMap, CodeBlockContext.PropAssignment, globalPropsTypeName, globalPropsInfo, varsMap, varsTypeMap);
        
        // Generate foreach loop
        // Use var for the loop variable to allow type inference
        sb.AppendLine($"{indent}foreach (var {forNode.VariableName} in {collectionExpression})");
        sb.AppendLine($"{indent}{{");
        
        // Render children inside the foreach block
        foreach (var child in forNode.Children)
        {
            GenerateNodeRendering(sb, child, indentLevel + 1, allScripts, allStyles, propsMap, globalPropsTypeName, globalPropsInfo, relativePath, globalComponents, varsMap, varsTypeMap, usedVariableNames, templatesRoot);
        }
        
        // Close foreach block
        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Escapes a string for use in a C# verbatim string literal.
    /// Escapes double quotes and converts non-ASCII Unicode characters to escape sequences.
    /// </summary>
    private static string EscapeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (c == '"')
            {
                // Escape double quotes for verbatim strings
                sb.Append("\"\"");
            }
            else if (c > 127)
            {
                // Escape non-ASCII Unicode characters as \uXXXX
                sb.Append($"\\u{(int)c:X4}");
            }
            else
            {
                // ASCII characters can be used as-is
                sb.Append(c);
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Escapes a string for use in a C# regular string literal (not verbatim).
    /// Escapes quotes, backslashes, and converts non-ASCII Unicode characters to escape sequences.
    /// </summary>
    private static string EscapeStringForRegularLiteral(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        // Fix common UTF-8 encoding issues where multi-byte characters are split
        // This handles cases where UTF-8 bytes are interpreted as separate characters
        input = FixUtf8EncodingIssues(input);
        
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c > 127)
                    {
                        // Escape non-ASCII Unicode characters as \uXXXX
                        sb.Append($"\\u{(int)c:X4}");
                    }
                    else
                    {
                        // ASCII characters can be used as-is
                        sb.Append(c);
                    }
                    break;
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Fixes UTF-8 encoding issues where multi-byte characters are incorrectly split.
    /// Converts corrupted UTF-8 byte sequences back to their correct Unicode characters.
    /// </summary>
    private static string FixUtf8EncodingIssues(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        // Common UTF-8 encoding issues:
        // • (U+2022) encoded as UTF-8 bytes: E2 80 A2
        // If read incorrectly, becomes: U+00E2 U+0080 U+00A2 (â + control + ¢)
        var result = input
            .Replace("\u00E2\u0080\u00A2", "\u2022") // Bullet (•)
            .Replace("\u00E2\u0080\u0093", "\u2013") // En dash (–)
            .Replace("\u00E2\u0080\u0094", "\u2014") // Em dash (—)
            .Replace("\u00E2\u0080\u0098", "\u2018") // Left single quotation mark (')
            .Replace("\u00E2\u0080\u0099", "\u2019") // Right single quotation mark (')
            .Replace("\u00E2\u0080\u009C", "\u201C") // Left double quotation mark (")
            .Replace("\u00E2\u0080\u009D", "\u201D") // Right double quotation mark (")
            .Replace("\u00E2\u0080\u00A6", "\u2026"); // Horizontal ellipsis (…)
        
        return result;
    }

    /// <summary>
    /// Escapes an attribute value for HTML.
    /// </summary>
    private static string EscapeAttributeValue(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>
    /// Converts a name to PascalCase.
    /// </summary>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpperInvariant(input[0]) + (input.Length > 1 ? input.Substring(1) : "");
    }

    /// <summary>
    /// Removes bracket notation from a segment name.
    /// [slug] -> slug, [...slugs] -> slugs
    /// </summary>
    private static string RemoveBracketNotationForComponent(string segment)
    {
        if (string.IsNullOrEmpty(segment))
            return segment;

        if (segment.StartsWith("[") && segment.EndsWith("]"))
        {
            var paramName = segment.Substring(1, segment.Length - 2);
            if (paramName.StartsWith("..."))
            {
                return paramName.Substring(3);
            }
            return paramName;
        }

        return segment;
    }
}

/// <summary>
/// Represents a component property definition.
/// </summary>
internal class ComponentPropInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
}

