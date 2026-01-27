using System.Text;
using Femur.Chtml.Parser;
using Femur.Markup.Abstractions.Nodes;
using Femur.Parsing.Nodes;

namespace Femur.Chtml.CSharpRenderer;

/// <summary>
/// Generates C# rendering code from CHTML AST.
/// Walks the AST and generates async render methods using RenderContext and MetaWriter.
/// </summary>
public class ChtmlCodeGenerator
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel;
    private readonly string _className;
    private readonly string _namespace;
    private readonly string _globalPropsTypeName;

    public ChtmlCodeGenerator(string className, string @namespace, string globalPropsTypeName = "EmptyPropsInstance")
    {
        _className = className;
        _namespace = @namespace;
        _globalPropsTypeName = globalPropsTypeName;
    }

    /// <summary>
    /// Generates the complete C# component code from a CHTML document.
    /// </summary>
    public string Generate(ChtmlDocumentNode document, bool isComponent = true, string? route = null,
        List<(string name, string type, bool isNullable)>? inputProps = null,
        List<(string name, string type, bool isNullable)>? computedProps = null)
    {
        _sb.Clear();
        _indentLevel = 0;

        GenerateUsings();
        _sb.AppendLine();

        _sb.AppendLine($"namespace {_namespace};");
        _sb.AppendLine();

        // Determine props types
        var hasInputProps = inputProps != null && inputProps.Any();
        var hasComputedProps = computedProps != null && computedProps.Any();
        string inputPropsType, propsType;
        var needsTransformation = hasComputedProps;

        if (hasComputedProps)
        {
            inputPropsType = hasInputProps ? $"{_className}InputProps" : "EmptyPropsInstance";
            propsType = $"{_className}Props";
        }
        else if (hasInputProps)
        {
            inputPropsType = $"{_className}Props";
            propsType = $"{_className}Props";
        }
        else
        {
            inputPropsType = "EmptyPropsInstance";
            propsType = "EmptyPropsInstance";
        }

        // Generate props classes if needed
        if (hasComputedProps)
        {
            if (hasInputProps)
            {
                GeneratePropsClass($"{_className}InputProps", inputProps!, null, false);
                _sb.AppendLine();
            }

            GeneratePropsClass($"{_className}Props", inputProps ?? new List<(string, string, bool)>(), computedProps!, hasInputProps);
            _sb.AppendLine();
        }
        else if (hasInputProps)
        {
            GeneratePropsClass($"{_className}Props", inputProps!, null, false);
            _sb.AppendLine();
        }

        var baseInterface = isComponent
            ? $"IRenderable<{inputPropsType}, {_globalPropsTypeName}>"
            : $"IRenderablePage<{inputPropsType}, {_globalPropsTypeName}>";

        _sb.AppendLine($"public partial class {_className} : {baseInterface}");
        _sb.AppendLine("{");
        _indentLevel++;

        if (!isComponent && route != null)
        {
            WriteLine($"public static string Route => \"{EscapeString(route)}\";");
            _sb.AppendLine();
        }

        WriteLine("public static Type[] DependsOn() => Array.Empty<Type>();");
        _sb.AppendLine();

        // Generate TransformProps method if needed
        if (needsTransformation)
        {
            WriteLine($"// Partial method stub: MUST be implemented in code-beside file (e.g., {_className}.partial.cs)");
            WriteLine($"private static partial {propsType} TransformProps({inputPropsType} inputProps, {_globalPropsTypeName} globalProps);");
            _sb.AppendLine();
        }

        WriteLine($"public static async ValueTask RenderAsync(RenderContext<{_globalPropsTypeName}> renderContext, {inputPropsType} inputProps, params RenderPipe<{_globalPropsTypeName}>[] children)");
        WriteLine("{");
        _indentLevel++;

        WriteLine("var (writer, globalProps) = renderContext;");
        _sb.AppendLine();

        // Transform props if needed
        if (needsTransformation)
        {
            WriteLine($"var props = TransformProps(inputProps, globalProps);");
            _sb.AppendLine();
        }
        else
        {
            WriteLine($"var props = inputProps;");
            _sb.AppendLine();
        }

        foreach (var child in document.Children)
        {
            GenerateNodeRendering(child);
        }

        _indentLevel--;
        WriteLine("}");

        _indentLevel--;
        _sb.AppendLine("}");

        return _sb.ToString();
    }

    private void GenerateUsings()
    {
        _sb.AppendLine("using System;");
        _sb.AppendLine("using System.Threading.Tasks;");
        _sb.AppendLine("using Femur.Chtml.Runtime;");
        _sb.AppendLine("using System.IO.Pipelines;");
        _sb.AppendLine("using Templates.Generated;");
    }

    private void GenerateNodeRendering(Node node)
    {
        switch (node)
        {
            case TextNode textNode:
                GenerateTextNodeRendering(textNode);
                break;
            case ElementNode elementNode:
                GenerateElementNodeRendering(elementNode);
                break;
            case ComponentNode componentNode:
                GenerateComponentNodeRendering(componentNode);
                break;
            case CodeNode codeNode:
                GenerateCodeNodeRendering(codeNode);
                break;
            case IfNode ifNode:
                GenerateIfNodeRendering(ifNode);
                break;
            case ForNode forNode:
                GenerateForNodeRendering(forNode);
                break;
            case ScriptNode scriptNode:
                GenerateScriptNodeRendering(scriptNode);
                break;
            case StyleNode styleNode:
                GenerateStyleNodeRendering(styleNode);
                break;
        }
    }

    private void GenerateTextNodeRendering(TextNode node)
    {
        var escapedContent = EscapeString(node.Content);
        WriteLine($"await writer.WriteAsync(\"{escapedContent}\");");
    }

    private void GenerateElementNodeRendering(ElementNode element)
    {
        var tagName = element.TagName;

        // Build opening tag with attributes
        WriteLine($"await writer.WriteAsync(\"<{tagName}\");");

        if (element.HasAttributes)
        {
            foreach (var (attrName, attrValue) in element.Attributes)
            {
                // Check if attribute value contains code blocks
                if (attrValue.StartsWith('{') && attrValue.EndsWith('}'))
                {
                    // Code block in attribute: attr={expression}
                    var codeContent = attrValue.Substring(1, attrValue.Length - 2).Trim();
                    WriteLine($"await writer.WriteAsync($\" {attrName}={{{codeContent}}}\");");
                }
                else
                {
                    // Regular string attribute
                    var escapedValue = EscapeString(attrValue);
                    WriteLine($"await writer.WriteAsync(\" {attrName}=\\\"{escapedValue}\\\"\");");
                }
            }
        }

        if (element.IsVoidElement || element.IsSelfClosing)
        {
            WriteLine("await writer.WriteAsync(\" />\");");
        }
        else
        {
            WriteLine("await writer.WriteAsync(\">\");");

            if (element.HasChildren)
            {
                foreach (var child in element.Children)
                {
                    GenerateNodeRendering(child);
                }
            }

            WriteLine($"await writer.WriteAsync(\"</{tagName}>\");");
        }
    }

    private void GenerateComponentNodeRendering(ComponentNode component)
    {
        var componentName = component.ComponentName;

        // Resolve component namespace from component name
        // For now, assume components are in Templates.Generated.Components namespace
        // TODO: Add proper namespace resolution based on component path
        var fullComponentName = $"Templates.Generated.Components.{componentName}";

        // Generate props instance (empty for now)
        // TODO: Parse attributes and generate props from them
        WriteLine($"await {fullComponentName}.RenderAsync(renderContext, new EmptyPropsInstance());");

        // TODO: Handle component children (RenderPipe[] children parameter)
    }

    private void GenerateCodeNodeRendering(CodeNode code)
    {
        var content = code.Content.Trim();

        // Handle special code blocks
        if (content.Equals("RenderChildren()", StringComparison.OrdinalIgnoreCase))
        {
            WriteLine("await renderContext.RenderAsync(children);");
            return;
        }

        // For now, treat all other code blocks as simple expressions
        // TODO: Add proper expression resolution (props.X, globalProps.X, etc.)
        WriteLine($"await writer.WriteAsync({content}?.ToString() ?? string.Empty);");
    }

    private void GenerateIfNodeRendering(IfNode ifNode)
    {
        WriteLine($"if ({ifNode.Condition})");
        WriteLine("{");
        _indentLevel++;

        if (ifNode.HasChildren)
        {
            foreach (var child in ifNode.Children)
            {
                GenerateNodeRendering(child);
            }
        }

        _indentLevel--;
        WriteLine("}");
    }

    private void GenerateForNodeRendering(ForNode forNode)
    {
        WriteLine($"foreach (var {forNode.VariableName} in {forNode.CollectionExpression})");
        WriteLine("{");
        _indentLevel++;

        if (forNode.HasChildren)
        {
            foreach (var child in forNode.Children)
            {
                GenerateNodeRendering(child);
            }
        }

        _indentLevel--;
        WriteLine("}");
    }

    private void GenerateScriptNodeRendering(ScriptNode script)
    {
        var openingTag = new StringBuilder("<script");
        if (script.HasAttributes)
        {
            foreach (var (attrName, attrValue) in script.Attributes)
            {
                var escapedValue = EscapeString(attrValue);
                openingTag.Append($" {attrName}=\"{escapedValue}\"");
            }
        }

        openingTag.Append('>');
        WriteLine($"await writer.WriteAsync(\"{EscapeString(openingTag.ToString())}\");");

        if (!string.IsNullOrEmpty(script.Content))
        {
            WriteLine($"await writer.WriteAsync(\"{EscapeString(script.Content)}\");");
        }

        WriteLine("await writer.WriteAsync(\"</script>\");");
    }

    private void GenerateStyleNodeRendering(StyleNode style)
    {
        var openingTag = new StringBuilder("<style");
        if (style.HasAttributes)
        {
            foreach (var (attrName, attrValue) in style.Attributes)
            {
                var escapedValue = EscapeString(attrValue);
                openingTag.Append($" {attrName}=\"{escapedValue}\"");
            }
        }

        openingTag.Append('>');
        WriteLine($"await writer.WriteAsync(\"{EscapeString(openingTag.ToString())}\");");

        if (!string.IsNullOrEmpty(style.Content))
        {
            WriteLine($"await writer.WriteAsync(\"{EscapeString(style.Content)}\");");
        }

        WriteLine("await writer.WriteAsync(\"</style>\");");
    }

    private void WriteLine(string text)
    {
        _sb.Append(new string(' ', _indentLevel * 4));
        _sb.AppendLine(text);
    }

    private void GeneratePropsClass(
        string className,
        List<(string name, string type, bool isNullable)> props,
        List<(string name, string type, bool isNullable)>? computedProps = null,
        bool inheritFromInput = false)
    {
        if (inheritFromInput && computedProps != null && computedProps.Any())
        {
            var baseClassName = className.Replace("Props", "InputProps");
            WriteLine($"public class {className} : {baseClassName}");
        }
        else
        {
            WriteLine($"public class {className}");
        }

        WriteLine("{");
        _indentLevel++;

        // Only generate input props if NOT inheriting from InputProps
        if (!inheritFromInput)
        {
            foreach (var (name, type, isNullable) in props)
            {
                var propName = StringUtils.ToPascalCase(name);
                var nullable = isNullable ? "?" : "";
                var required = isNullable ? "" : "required ";
                var typeName = NormalizeRenderPipeType(type);
                WriteLine($"public {required}{typeName}{nullable} {propName} {{ get; set; }}");
            }
        }

        // Add computed props if provided
        if (computedProps != null)
        {
            var basePropsSet = inheritFromInput && props != null
                ? new HashSet<string>(props.Select(p => StringUtils.ToPascalCase(p.name)), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, type, isNullable) in computedProps)
            {
                var propName = StringUtils.ToPascalCase(name);
                var isHidingBaseProp = basePropsSet.Contains(propName);
                var nullable = (isNullable || isHidingBaseProp) ? "?" : "";
                var required = (isNullable || isHidingBaseProp) ? "" : "required ";
                var typeName = NormalizeRenderPipeType(type);
                var newKeyword = isHidingBaseProp ? "new " : "";

                WriteLine($"public {newKeyword}{required}{typeName}{nullable} {propName} {{ get; set; }}");
            }
        }

        _indentLevel--;
        WriteLine("}");
    }

    private string NormalizeRenderPipeType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return typeName;
        }

        var trimmed = typeName.Trim();
        if (trimmed.Equals("RenderPipe", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Femur.Chtml.Runtime.RenderPipe", StringComparison.OrdinalIgnoreCase))
        {
            return $"RenderPipe<{_globalPropsTypeName}>";
        }

        return typeName;
    }

    private static string EscapeString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}