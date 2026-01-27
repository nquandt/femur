using System.Text;
using Femur.Chtml.CSharpRenderer;
using Femur.Chtml.Parser;

// Simple CLI: dotnet run -- [templatesRoot]
// If no argument provided, look for Templates folder in current working directory
var templatesRoot = args.Length > 0
    ? args[0]
    : Path.Combine(Directory.GetCurrentDirectory(), "Templates");

if (!Path.IsPathRooted(templatesRoot))
{
    templatesRoot = Path.GetFullPath(templatesRoot);
}

if (!Directory.Exists(templatesRoot))
{
    await Console.Error.WriteLineAsync($"Templates root not found: {templatesRoot}");
    return 1;
}

// Clean up existing generated files
var generatedRoot = Path.Combine(templatesRoot, ".generated");
if (Directory.Exists(generatedRoot))
{
    await Console.Out.WriteLineAsync("Cleaning up existing generated files...");
    Directory.Delete(generatedRoot, recursive: true);
}

// Process global.chtml to generate GlobalProps class
var globalChtmlPath = Path.Combine(templatesRoot, "global.chtml");
var globalHtmlPath = Path.Combine(templatesRoot, "global.html");
var globalProps = new List<(string name, string type, bool isNullable)>();
var globalPropsTypeName = "EmptyPropsInstance";

var globalPath = File.Exists(globalChtmlPath) ? globalChtmlPath :
                (File.Exists(globalHtmlPath) ? globalHtmlPath : null);

if (globalPath != null)
{
    await Console.Out.WriteLineAsync($"Parsing {Path.GetFileName(globalPath)}");
    var globalText = await File.ReadAllTextAsync(globalPath, Encoding.UTF8);
    var (globalFrontMatter, _) = FrontMatterParser.Split(globalText);
    var globalFrontMatterDict = FrontMatterParser.Parse(globalFrontMatter);
    globalProps = FrontMatterParser.ParseProps(globalFrontMatterDict, "GlobalProps");

    if (globalProps.Any())
    {
        globalPropsTypeName = "Templates.Generated.GlobalProps";
    }
}

// Generate GlobalProps class
var globalPropsCode = GlobalPropsCodeGenerator.Generate(globalProps);
Directory.CreateDirectory(generatedRoot);
var globalPropsPath = Path.Combine(generatedRoot, "GlobalProps.cs");
await File.WriteAllTextAsync(globalPropsPath, globalPropsCode, Encoding.UTF8);
await Console.Out.WriteLineAsync($"Generated: GlobalProps.cs");

// Find all .chtml and .html files
var files = Directory.GetFiles(templatesRoot, "*", SearchOption.AllDirectories)
    .Where(f =>
    {
        var ext = Path.GetExtension(f).ToLowerInvariant();
        if (ext != ".chtml" && ext != ".html")
        {
            return false;
        }

        // Exclude files in wwwroot directories (these are static files, not templates)
        var relativePath = Path.GetRelativePath(templatesRoot, f).Replace(Path.DirectorySeparatorChar, '/');
        if (relativePath.Contains("/wwwroot/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Skip global.chtml/html files (already processed)
        var fileName = Path.GetFileName(f);
        if (fileName.Equals("global.chtml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("global.html", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    })
    .ToArray();

if (files.Length == 0)
{
    await Console.Out.WriteLineAsync("No template files found.");
    return 0;
}

await Console.Out.WriteLineAsync($"Found {files.Length} template file(s).");

// Process each template file
foreach (var filePath in files)
{
    var relativePath = Path.GetRelativePath(templatesRoot, filePath).Replace(Path.DirectorySeparatorChar, '/');
    await Console.Out.WriteLineAsync($"Processing: {relativePath}");

    // Read file and split front matter
    var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
    var (frontMatter, body) = FrontMatterParser.Split(text);
    var frontMatterDict = FrontMatterParser.Parse(frontMatter);

    // Determine if this is a component or page based on path
    var isComponent = relativePath.Contains("components/", StringComparison.OrdinalIgnoreCase);
    var isPage = relativePath.Contains("pages/", StringComparison.OrdinalIgnoreCase);

    // Extract namespace parts from path
    // components/container/index.chtml -> Templates.Generated.Components.Container
    // pages/about/index.chtml -> Templates.Generated.Pages.About
    var pathParts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var nsParts = new List<string> { "Templates", "Generated" };

    if (isComponent)
    {
        nsParts.Add("Components");
        // Find "components" in path
        var componentsIndex = -1;
        for (var i = 0; i < pathParts.Length; i++)
        {
            if (pathParts[i].Equals("components", StringComparison.OrdinalIgnoreCase))
            {
                componentsIndex = i;
                break;
            }
        }

        if (componentsIndex >= 0)
        {
            // Add folder names after "components" up to filename
            for (var i = componentsIndex + 1; i < pathParts.Length - 1; i++)
            {
                var segment = RemoveBracketNotation(pathParts[i]);
                nsParts.Add(StringUtils.ToPascalCase(segment));
            }
        }
    }
    else if (isPage)
    {
        nsParts.Add("Pages");
        // Find "pages" in path
        var pagesIndex = -1;
        for (var i = 0; i < pathParts.Length; i++)
        {
            if (pathParts[i].Equals("pages", StringComparison.OrdinalIgnoreCase))
            {
                pagesIndex = i;
                break;
            }
        }

        if (pagesIndex >= 0)
        {
            // Add folder names after "pages" up to filename
            for (var i = pagesIndex + 1; i < pathParts.Length - 1; i++)
            {
                var segment = RemoveBracketNotation(pathParts[i]);
                nsParts.Add(StringUtils.ToPascalCase(segment));
            }
        }
    }
    else
    {
        // Default to Components if unclear
        nsParts.Add("Components");
    }

    var @namespace = string.Join(".", nsParts);
    var fileName = Path.GetFileNameWithoutExtension(pathParts[^1]);
    fileName = RemoveBracketNotation(fileName);

    // For index files, always use "Index"
    var className = fileName.Equals("index", StringComparison.OrdinalIgnoreCase)
        ? "Index"
        : StringUtils.ToPascalCase(fileName);

    // Determine route for pages
    string? route = null;
    if (isPage)
    {
        route = RouteGenerator.GenerateFromPath(relativePath);
    }

    // Parse the template body
    ChtmlDocumentNode document;
    var bodyBytes = Encoding.UTF8.GetBytes(body);
    using (var stream = new MemoryStream(bodyBytes))
    {
        var parser = new ChtmlParser(stream);
        document = parser.Parse();
    }

    // Parse props from front matter
    var inputProps = FrontMatterParser.ParseProps(frontMatterDict, "Props");
    var computedProps = FrontMatterParser.ParseProps(frontMatterDict, "ComputedProps");

    // Generate C# code
    var generator = new ChtmlCodeGenerator(className, @namespace, globalPropsTypeName);
    var code = generator.Generate(document, isComponent: isComponent, route: route,
        inputProps: inputProps, computedProps: computedProps);

    // Write generated file
    var generatedDir = Path.Combine(templatesRoot, ".generated", Path.GetDirectoryName(relativePath) ?? "");
    Directory.CreateDirectory(generatedDir);

    var outputFileName = className + ".cs";
    var outPath = Path.Combine(generatedDir, outputFileName);
    await File.WriteAllTextAsync(outPath, code, Encoding.UTF8);

    await Console.Out.WriteLineAsync($"  Generated: {Path.GetRelativePath(templatesRoot, outPath)}");
}

static string RemoveBracketNotation(string segment)
{
    if (string.IsNullOrEmpty(segment))
    {
        return segment;
    }

    if (segment.StartsWith('[') && segment.EndsWith(']'))
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

await Console.Out.WriteLineAsync("Done!");
return 0;

