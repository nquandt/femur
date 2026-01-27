using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tokenizer;

namespace ChtmlCompiler;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Simple CLI: dotnet run -- [templatesRoot]
        var templatesRoot = args.Length > 0 ? args[0] : Path.Combine("..", "..", "Templates");
        if (!Path.IsPathRooted(templatesRoot))
        {
            templatesRoot = Path.GetFullPath(templatesRoot);
        }

        if (!Directory.Exists(templatesRoot))
        {
            Console.Error.WriteLine($"Templates root not found: {templatesRoot}");
            return 1;
        }

        // Initialize Shiki processor for code element highlighting during compilation
        // Try to find shiki-transform.js in common locations
        string? shikiScriptPath = null;
        var scriptPath1 = Path.Combine(templatesRoot, "..", "shiki-transform.js");
        var scriptPath2 = Path.Combine(templatesRoot, "..", "..", "shiki-transform.js");
        var scriptPath3 = Path.Combine(templatesRoot, "shiki-transform.js");
        
        // Resolve relative paths to absolute
        if (File.Exists(scriptPath1))
        {
            shikiScriptPath = Path.GetFullPath(scriptPath1);
        }
        else if (File.Exists(scriptPath2))
        {
            shikiScriptPath = Path.GetFullPath(scriptPath2);
        }
        else if (File.Exists(scriptPath3))
        {
            shikiScriptPath = Path.GetFullPath(scriptPath3);
        }
        
        if (shikiScriptPath != null)
        {
            Console.WriteLine($"[ShikiProcessor] Initializing with script: {shikiScriptPath}");
        }
        else
        {
            Console.WriteLine($"[ShikiProcessor] Warning: shiki-transform.js not found. Shiki highlighting will be disabled.");
        }
        
        ShikiProcessor.Initialize(shikiScriptPath);

        var files = Directory.GetFiles(templatesRoot, "*", SearchOption.AllDirectories)
            .Where(f => {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".chtml" && ext != ".html")
                    return false;
                
                // Exclude files in wwwroot directories (these are static files, not templates)
                var relativePath = Path.GetRelativePath(templatesRoot, f).Replace(Path.DirectorySeparatorChar, '/');
                if (relativePath.Contains("/wwwroot/", StringComparison.OrdinalIgnoreCase) ||
                    relativePath.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
                    return false;
                
                return true;
            })
            .ToArray();
        if (files.Length == 0)
        {
            Console.WriteLine("No .chtml or .html files found.");
            return 0;
        }

        // Parse global.chtml to generate GlobalProps class and determine type
        var globalProps = await ProcessGlobalProps(templatesRoot);
        var globalPropsTypeName = globalProps.Any() 
            ? "Templates.Generated.GlobalProps" 
            : "Shared.Generated.EmptyPropsInstance";

        var pages = new List<(string className, string route, string ns, string? inputPropsType, string globalPropsType, List<string> routeParameters)>();
        
        // Track global components (components with GlobalAs in frontmatter)
        var globalComponents = new Dictionary<string, string>(); // globalName -> className

        // First pass: collect all scripts and styles from all files before code generation
        var allScripts = await CollectAllScripts(files, templatesRoot);
        var allStyles = await CollectAllStyles(files, templatesRoot);
        
        // Track markdown files referenced via LoadMarkdown
        var markdownFiles = new HashSet<string>();
        var markdownClassMap = new Dictionary<string, string>(); // normalizedPath -> className
        
        // Track _content folders for props generation
        var contentFolders = new HashSet<string>(); // normalized folder path -> will generate props class
        
        // Track markdown files found via LoadMarkdownCollectionByLanguage patterns
        var collectionMarkdownFiles = new HashSet<string>(); // normalized paths to markdown files in subdirectories

        // Second pass: generate code with all scripts collected
        // First, scan for markdown references and _content folders
        foreach (var file in files)
        {
            // Skip global.chtml in the main processing loop
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("global.chtml", StringComparison.OrdinalIgnoreCase) || 
                fileName.Equals("global.html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var text = await File.ReadAllTextAsync(file, Encoding.UTF8);
            var (frontMatter, body) = FrontMatterParser.Split(text);
            var frontMatterDict = FrontMatterParser.Parse(frontMatter);
            
            var relative = Path.GetRelativePath(templatesRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            
            // Parse vars to find LoadMarkdown references and _content folders
            var vars = CodeGenerator.ParseVars(frontMatterDict, relative, markdownFiles, contentFolders, collectionMarkdownFiles);
        }
        
        // Generate props classes for each _content folder
        var contentPropsMap = new Dictionary<string, string>(); // folder path -> props class name
        foreach (var contentFolderPath in contentFolders)
        {
            var fullContentFolderPath = Path.Combine(templatesRoot, contentFolderPath);
            
            // Determine props class name and namespace
            // For "pages/_content" -> "ContentProps" in "Templates.Generated.Pages"
            // For "components/about/_content" -> "ContentProps" in "Templates.Generated.Components.About"
            // All _content folders use "ContentProps" as the class name
            var folderParts = contentFolderPath.Split('/');
            var propsClassName = "ContentProps";
            
            // Determine namespace (exclude _content folder itself)
            // Use PascalCase for namespace (C# requirement)
            var nsParts = new List<string> { "Templates", "Generated" };
            for (int i = 0; i < folderParts.Length - 1; i++) // Exclude _content itself
            {
                if (!string.IsNullOrEmpty(folderParts[i]) && !folderParts[i].Equals("_content", StringComparison.OrdinalIgnoreCase))
                {
                    nsParts.Add(ToPascalCase(folderParts[i]));
                }
            }
            var propsNamespace = string.Join(".", nsParts);
            
            contentPropsMap[contentFolderPath] = $"{propsNamespace}.{propsClassName}";
            
            // Generate props class
            var propsCode = ContentPropsGenerator.Generate(fullContentFolderPath, templatesRoot, propsClassName, propsNamespace);
            
            // Write generated file preserving original folder casing (lowercase to match source)
            // For "pages/_content" -> ".generated/pages/ContentProps.cs"
            // For "pages/about/_content" -> ".generated/pages/about/ContentProps.cs"
            var propsOutDir = Path.Combine(templatesRoot, ".generated");
            // Use original folder parts (preserve casing) instead of namespace parts
            for (int i = 0; i < folderParts.Length - 1; i++) // Exclude _content itself
            {
                if (!string.IsNullOrEmpty(folderParts[i]) && !folderParts[i].Equals("_content", StringComparison.OrdinalIgnoreCase))
                {
                    propsOutDir = Path.Combine(propsOutDir, folderParts[i]); // Use original casing
                }
            }
            Directory.CreateDirectory(propsOutDir);
            var propsOutPath = Path.Combine(propsOutDir, propsClassName + ".cs");
            await File.WriteAllTextAsync(propsOutPath, propsCode, Encoding.UTF8);
            Console.WriteLine($"Wrote {propsOutPath}");
            
            // Generate static classes for each markdown file in this _content folder
            var markdownFilesInFolder = Directory.GetFiles(fullContentFolderPath, "*.md", SearchOption.TopDirectoryOnly);
            foreach (var markdownFile in markdownFilesInFolder)
            {
                var languageCode = Path.GetFileNameWithoutExtension(markdownFile);
                var languageClassName = ToPascalCase(languageCode); // e.g., "En", "Es"
                
                // Generate static class for this language file
                var markdownClassCode = ContentMarkdownGenerator.Generate(
                    markdownFile, 
                    templatesRoot, 
                    languageClassName, 
                    propsNamespace, 
                    $"{propsNamespace}.{propsClassName}"
                );
                
                // Write to same directory as ContentProps
                var markdownClassPath = Path.Combine(propsOutDir, $"{languageClassName}.cs");
                await File.WriteAllTextAsync(markdownClassPath, markdownClassCode, Encoding.UTF8);
                Console.WriteLine($"Wrote {markdownClassPath}");
                
                // Track this class for LoadMarkdownByLanguage references
                // Key: normalized path like "pages/about/_content/en.md"
                // Value: full class name like "Templates.Generated.Pages.About.En"
                var relativeMarkdownPath = Path.GetRelativePath(templatesRoot, markdownFile).Replace(Path.DirectorySeparatorChar, '/');
                if (!markdownClassMap.ContainsKey(relativeMarkdownPath))
                {
                    markdownClassMap[relativeMarkdownPath] = $"{propsNamespace}.{languageClassName}";
                    Console.WriteLine($"  Mapped markdown: {relativeMarkdownPath} -> {propsNamespace}.{languageClassName}");
                }
            }
        }
        
        // Generate static classes for markdown files found via LoadMarkdownCollectionByLanguage patterns
        foreach (var patternMarker in collectionMarkdownFiles)
        {
            if (!patternMarker.StartsWith("PATTERN:", StringComparison.OrdinalIgnoreCase))
                continue;
                
            var normalizedPattern = patternMarker.Substring("PATTERN:".Length);
            
            // Extract language placeholder location
            var langPlaceholder = "{lang}";
            var langPlaceholderIndex = normalizedPattern.IndexOf(langPlaceholder, StringComparison.OrdinalIgnoreCase);
            if (langPlaceholderIndex == -1)
                continue;
                
            var beforeLang = normalizedPattern.Substring(0, langPlaceholderIndex);
            var afterLang = normalizedPattern.Substring(langPlaceholderIndex + langPlaceholder.Length);
            
            // Check if pattern contains ** (recursive search)
            var isRecursive = normalizedPattern.Contains("**");
            
            // Build search directory
            var searchBaseDir = beforeLang;
            if (searchBaseDir.Contains("**"))
            {
                searchBaseDir = searchBaseDir.Replace("**", "").TrimEnd('/');
            }
            
            var searchDir = Path.Combine(templatesRoot, searchBaseDir);
            if (!Directory.Exists(searchDir))
                continue;
            
            // Find all matching files recursively
            var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allMdFiles = Directory.GetFiles(searchDir, "*.md", searchOption)
                .Select(f => Path.GetRelativePath(templatesRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
                .ToList();
            
            // Find files matching the pattern
            foreach (var file in allMdFiles)
            {
                if (file.Contains("_content") && file.EndsWith(afterLang))
                {
                    // Extract language code from filename
                    var fileName = Path.GetFileName(file);
                    var langCode = Path.GetFileNameWithoutExtension(fileName);
                    
                    // Determine the _content folder this file belongs to
                    var contentIndex = file.IndexOf("/_content/", StringComparison.OrdinalIgnoreCase);
                    if (contentIndex < 0)
                        continue;
                        
                    var contentFolderPath = file.Substring(0, contentIndex + "/_content/".Length - 1);
                    
                    // Get or create ContentProps class name for this folder
                    if (!contentPropsMap.TryGetValue(contentFolderPath, out var existingPropsTypeName))
                    {
                        // Generate props class for this folder if not already done
                        var folderParts = contentFolderPath.Split('/');
                        var collectionPropsClassName = "ContentProps";
                        var collectionNsParts = new List<string> { "Templates", "Generated" };
                        for (int i = 0; i < folderParts.Length - 1; i++)
                        {
                            if (!string.IsNullOrEmpty(folderParts[i]) && !folderParts[i].Equals("_content", StringComparison.OrdinalIgnoreCase))
                            {
                                collectionNsParts.Add(ToPascalCase(folderParts[i]));
                            }
                        }
                        var collectionPropsNamespace = string.Join(".", collectionNsParts);
                        existingPropsTypeName = $"{collectionPropsNamespace}.{collectionPropsClassName}";
                        contentPropsMap[contentFolderPath] = existingPropsTypeName;
                        
                        // Generate props class
                        var fullContentFolderPath = Path.Combine(templatesRoot, contentFolderPath);
                        var propsCode = ContentPropsGenerator.Generate(fullContentFolderPath, templatesRoot, collectionPropsClassName, collectionPropsNamespace);
                        
                        // Write props class
                        var propsOutDir = Path.Combine(templatesRoot, ".generated");
                        for (int i = 0; i < folderParts.Length - 1; i++)
                        {
                            if (!string.IsNullOrEmpty(folderParts[i]) && !folderParts[i].Equals("_content", StringComparison.OrdinalIgnoreCase))
                            {
                                propsOutDir = Path.Combine(propsOutDir, folderParts[i]);
                            }
                        }
                        Directory.CreateDirectory(propsOutDir);
                        var propsOutPath = Path.Combine(propsOutDir, collectionPropsClassName + ".cs");
                        await File.WriteAllTextAsync(propsOutPath, propsCode, Encoding.UTF8);
                        Console.WriteLine($"Wrote {propsOutPath}");
                    }
                    
                    // Generate class name from subdirectory + language (e.g., "_01En", "02En")
                    // If subdirectory name starts with a number, prefix with underscore for valid C# identifier
                    var fileDir = Path.GetDirectoryName(file)?.Replace(Path.DirectorySeparatorChar, '/') ?? "";
                    var subDirName = fileDir.Split('/').LastOrDefault() ?? "";
                    var pascalSubDir = ToPascalCase(subDirName);
                    // If starts with digit, prefix with underscore
                    if (pascalSubDir.Length > 0 && char.IsDigit(pascalSubDir[0]))
                    {
                        pascalSubDir = "_" + pascalSubDir;
                    }
                    var className = pascalSubDir + ToPascalCase(langCode);
                    
                    // Determine namespace
                    var fileDirParts = fileDir.Split('/');
                    var collectionMarkdownNsParts = new List<string> { "Templates", "Generated" };
                    foreach (var part in fileDirParts)
                    {
                        if (!string.IsNullOrEmpty(part) && !part.Equals("_content", StringComparison.OrdinalIgnoreCase))
                        {
                            var pascalPart = ToPascalCase(part);
                            // If starts with digit, prefix with underscore for valid C# namespace
                            if (pascalPart.Length > 0 && char.IsDigit(pascalPart[0]))
                            {
                                pascalPart = "_" + pascalPart;
                            }
                            collectionMarkdownNsParts.Add(pascalPart);
                        }
                    }
                    var markdownNamespace = string.Join(".", collectionMarkdownNsParts);
                    
                    // Extract props namespace from contentFolderPath (reuse from above if available)
                    var contentFolderParts = contentFolderPath.Split('/');
                    var finalPropsNsParts = new List<string> { "Templates", "Generated" };
                    for (int i = 0; i < contentFolderParts.Length - 1; i++)
                    {
                        if (!string.IsNullOrEmpty(contentFolderParts[i]) && !contentFolderParts[i].Equals("_content", StringComparison.OrdinalIgnoreCase))
                        {
                            finalPropsNsParts.Add(ToPascalCase(contentFolderParts[i]));
                        }
                    }
                    var finalPropsNamespace = string.Join(".", finalPropsNsParts);
                    var finalPropsClassName = "ContentProps";
                    var fullPropsTypeName = $"{finalPropsNamespace}.{finalPropsClassName}";
                    
                    // Generate static class for this markdown file
                    var fullFilePath = Path.Combine(templatesRoot, file);
                    var markdownClassCode = ContentMarkdownGenerator.Generate(
                        fullFilePath,
                        templatesRoot,
                        className,
                        markdownNamespace,
                        fullPropsTypeName
                    );
                    
                    // Write generated file
                    var markdownOutDir = Path.Combine(templatesRoot, ".generated");
                    foreach (var part in fileDirParts)
                    {
                        if (!string.IsNullOrEmpty(part) && !part.Equals("_content", StringComparison.OrdinalIgnoreCase))
                        {
                            markdownOutDir = Path.Combine(markdownOutDir, part);
                        }
                    }
                    Directory.CreateDirectory(markdownOutDir);
                    var markdownOutPath = Path.Combine(markdownOutDir, className + ".cs");
                    await File.WriteAllTextAsync(markdownOutPath, markdownClassCode, Encoding.UTF8);
                    Console.WriteLine($"Wrote {markdownOutPath}");
                    
                    // Track this class in markdownClassMap
                    if (!markdownClassMap.ContainsKey(file))
                    {
                        markdownClassMap[file] = $"{markdownNamespace}.{className}";
                        Console.WriteLine($"  Mapped collection markdown: {file} -> {markdownNamespace}.{className}");
                    }
                }
            }
        }
        
        // Generate static classes for each referenced markdown file
        foreach (var markdownPath in markdownFiles)
        {
            var fullPath = Path.Combine(templatesRoot, markdownPath);
            
            // Generate class name from filename only (not full path)
            var fileName = Path.GetFileNameWithoutExtension(markdownPath);
            var className = ToPascalCase(fileName);
            
            // Determine namespace from directory structure (same as pages/components)
            var markdownDir = Path.GetDirectoryName(markdownPath)?.Replace(Path.DirectorySeparatorChar, '/');
            string markdownNamespace;
            if (string.IsNullOrEmpty(markdownDir))
            {
                // Root level markdown file
                markdownNamespace = "Templates.Generated";
            }
            else
            {
                // Build namespace from directory structure
                var nsParts = new List<string> { "Templates", "Generated" };
                var dirParts = markdownDir.Split('/');
                foreach (var part in dirParts)
                {
                    if (!string.IsNullOrEmpty(part))
                    {
                        var pascalPart = ToPascalCase(part);
                        // If starts with digit, prefix with underscore for valid C# namespace
                        if (pascalPart.Length > 0 && char.IsDigit(pascalPart[0]))
                        {
                            pascalPart = "_" + pascalPart;
                        }
                        nsParts.Add(pascalPart);
                    }
                }
                markdownNamespace = string.Join(".", nsParts);
            }
            
            markdownClassMap[markdownPath] = $"{markdownNamespace}.{className}";
            
            // Generate static class (will generate stub if file doesn't exist)
            var markdownCode = MarkdownGenerator.Generate(fullPath, templatesRoot, className, markdownNamespace);
            
            // Write generated file in same directory structure as original
            var markdownRelativeDir = Path.GetDirectoryName(markdownPath);
            var markdownOutDir = string.IsNullOrEmpty(markdownRelativeDir)
                ? Path.Combine(templatesRoot, ".generated")
                : Path.Combine(templatesRoot, ".generated", markdownRelativeDir);
            Directory.CreateDirectory(markdownOutDir);
            var markdownOutPath = Path.Combine(markdownOutDir, fileName + ".cs");
            await File.WriteAllTextAsync(markdownOutPath, markdownCode, Encoding.UTF8);
            Console.WriteLine($"Wrote {markdownOutPath}");
            
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"  Warning: Markdown file not found: {fullPath} (generated stub class)");
            }
        }

        // Third pass: generate code with markdown class map
        foreach (var file in files)
        {
            // Skip global.chtml in the main processing loop
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("global.chtml", StringComparison.OrdinalIgnoreCase) || 
                fileName.Equals("global.html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            Console.WriteLine($"Processing {file}");
            
            var text = await File.ReadAllTextAsync(file, Encoding.UTF8);
            var (frontMatter, body) = FrontMatterParser.Split(text);
            var frontMatterDict = FrontMatterParser.Parse(frontMatter);

            var relative = Path.GetRelativePath(templatesRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            var isComponent = relative.Contains("components/", StringComparison.OrdinalIgnoreCase);

            // Parse the HTML body using ChtmlParser
            var bytes = Encoding.UTF8.GetBytes(body);
            var document = ChtmlParser.Parse(bytes);

            // Determine component/page name and namespace
            var className = DetermineClassName(file, isComponent, relative);
            var ns = DetermineNamespace(file, isComponent, relative);
            
            // Track global components
            if (isComponent)
            {
                if (frontMatterDict != null && frontMatterDict.TryGetValue("GlobalAs", out var globalAsObj))
                {
                    var globalName = globalAsObj?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(globalName))
                    {
                        var fullClassName = $"{ns}.{className}";
                        globalComponents[globalName] = fullClassName;
                        Console.WriteLine($"  Registered global component: {globalName} -> {fullClassName}");
                    }
                }
            }

            // Get route for pages
            var route = DetermineRoute(isComponent, frontMatterDict, relative);
            
            // Extract route parameters from file path (bracket notation)
            Dictionary<string, object>? finalFrontMatter = frontMatterDict;
            if (!isComponent)
            {
                // Extract parameters from the file path itself (bracket notation)
                var routeParameters = RouteGenerator.ExtractParametersFromPath(relative);
                if (routeParameters.Count > 0)
                {
                    finalFrontMatter = MergeRouteParametersWithProps(frontMatterDict, routeParameters, className);
                }
            }

            // Generate code with all scripts already collected and markdown class map
            var generated = CodeGenerator.Generate(document, className, ns, finalFrontMatter, isComponent, route, allScripts, allStyles, globalPropsTypeName, globalProps, relative, globalComponents, markdownFiles, markdownClassMap, templatesRoot);

            // Write generated file
            var relativePath = Path.GetRelativePath(templatesRoot, file);
            var generatedDir = Path.Combine(templatesRoot, ".generated", Path.GetDirectoryName(relativePath) ?? "");
            Directory.CreateDirectory(generatedDir);
            
            // Handle both .chtml and .html extensions
            var outputFileName = Path.GetFileNameWithoutExtension(file);
            var outPath = Path.Combine(generatedDir, outputFileName + ".cs");
            await File.WriteAllTextAsync(outPath, generated, Encoding.UTF8);
            Console.WriteLine($"Wrote {outPath}");

            // Track pages for route registration (include props info)
            if (!isComponent)
            {
                var pageRoute = route ?? RouteGenerator.GenerateFromPath(relative);
                // Extract route parameters from file path (bracket notation)
                var routeParameters = RouteGenerator.ExtractParametersFromPath(relative);
                
                // Merge route parameters with front matter props
                var mergedProps = MergeRouteParametersWithProps(frontMatterDict, routeParameters, className);
                
                var inputPropsType = DetermineInputPropsType(frontMatterDict, className, mergedProps);
                pages.Add((className, pageRoute, ns, inputPropsType, globalPropsTypeName, routeParameters));
            }
        }

        // Generate global component registry if we have global components
        if (globalComponents.Any())
        {
            var globalRegistryCode = GlobalComponentRegistryGenerator.GenerateRegistry(globalComponents);
            var globalRegistryPath = Path.Combine(templatesRoot, ".generated", "GlobalComponentRegistry.cs");
            await File.WriteAllTextAsync(globalRegistryPath, globalRegistryCode, Encoding.UTF8);
            Console.WriteLine($"Wrote {globalRegistryPath}");
        }

        // Generate route registration if we have pages
        if (pages.Any())
        {
            var routeRegistration = RegistrationCodeGenerator.GenerateRouteRegistration(pages);
            var routeRegistrationPath = Path.Combine(templatesRoot, ".generated", "RouteRegistration.cs");
            await File.WriteAllTextAsync(routeRegistrationPath, routeRegistration, Encoding.UTF8);
            Console.WriteLine($"Wrote {routeRegistrationPath}");
        }

        // Generate style registry and route registration
        // Always generate StyleRegistry even if empty, so the class exists
        var styleRegistryCode = StyleCodeGenerator.GenerateRegistry(allStyles);
        var styleRegistryPath = Path.Combine(templatesRoot, ".generated", "StyleRegistry.cs");
        await File.WriteAllTextAsync(styleRegistryPath, styleRegistryCode, Encoding.UTF8);
        Console.WriteLine($"Wrote {styleRegistryPath}");

        var styleRouteRegistration = StyleCodeGenerator.GenerateRouteRegistration(allStyles);
        var styleRouteRegistrationPath = Path.Combine(templatesRoot, ".generated", "StyleRouteRegistration.cs");
        await File.WriteAllTextAsync(styleRouteRegistrationPath, styleRouteRegistration, Encoding.UTF8);
        Console.WriteLine($"Wrote {styleRouteRegistrationPath}");

        // Generate script registry and route registration
        // Always generate ScriptRegistry even if empty, so the class exists
        var scriptRegistryCode = ScriptCodeGenerator.GenerateRegistry(allScripts);
        var scriptRegistryPath = Path.Combine(templatesRoot, ".generated", "ScriptRegistry.cs");
        await File.WriteAllTextAsync(scriptRegistryPath, scriptRegistryCode, Encoding.UTF8);
        Console.WriteLine($"Wrote {scriptRegistryPath}");

        var scriptRouteRegistration = ScriptCodeGenerator.GenerateRouteRegistration(allScripts);
        var scriptRouteRegistrationPath = Path.Combine(templatesRoot, ".generated", "ScriptRouteRegistration.cs");
        await File.WriteAllTextAsync(scriptRouteRegistrationPath, scriptRouteRegistration, Encoding.UTF8);
        Console.WriteLine($"Wrote {scriptRouteRegistrationPath}");

        // Generate TemplateFunctions helper class (always generate, so it exists)
        var templateFunctionsCode = TemplateFunctionsGenerator.Generate();
        var templateFunctionsPath = Path.Combine(templatesRoot, ".generated", "TemplateFunctions.cs");
        await File.WriteAllTextAsync(templateFunctionsPath, templateFunctionsCode, Encoding.UTF8);
        Console.WriteLine($"Wrote {templateFunctionsPath}");

        return 0;
    }

    /// <summary>
    /// Processes global.chtml file and generates GlobalProps class.
    /// </summary>
    private static async Task<List<(string name, string type, bool isNullable)>> ProcessGlobalProps(string templatesRoot)
    {
        var globalChtmlPath = Path.Combine(templatesRoot, "global.chtml");
        var globalHtmlPath = Path.Combine(templatesRoot, "global.html");
        var globalProps = new List<(string name, string type, bool isNullable)>();

        var globalPath = File.Exists(globalChtmlPath) ? globalChtmlPath : 
                        (File.Exists(globalHtmlPath) ? globalHtmlPath : null);

        if (globalPath != null)
        {
            Console.WriteLine($"Parsing {Path.GetFileName(globalPath)}");
            var globalText = await File.ReadAllTextAsync(globalPath, Encoding.UTF8);
            var (globalFrontMatter, _) = FrontMatterParser.Split(globalText);
            var globalFrontMatterDict = FrontMatterParser.Parse(globalFrontMatter);
            globalProps = FrontMatterParser.ParseProps(globalFrontMatterDict, "GlobalProps");
        }
        else
        {
            Console.WriteLine("No global.chtml or global.html found - using EmptyPropsInstance for global props");
        }

        // Generate GlobalProps class - write to Templates/.generated
        var globalPropsCode = GlobalPropsCodeGenerator.Generate(globalProps);
        var globalPropsDir = Path.Combine(templatesRoot, ".generated");
        Directory.CreateDirectory(globalPropsDir);
        var globalPropsPath = Path.Combine(globalPropsDir, "GlobalProps.cs");
        await File.WriteAllTextAsync(globalPropsPath, globalPropsCode, Encoding.UTF8);
        Console.WriteLine($"Wrote {globalPropsPath}");

        return globalProps;
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
    /// Converts a string to PascalCase.
    /// </summary>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        // Split by common separators and capitalize each part
        var parts = input.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part.Substring(1));
                }
            }
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Collects all scripts from template files, deduplicating by content.
    /// </summary>
    private static async Task<List<(string id, string content)>> CollectAllScripts(string[] files, string templatesRoot)
    {
        var allScripts = new List<(string id, string content)>();

        foreach (var file in files)
        {
            // Skip global.chtml in script collection
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("global.chtml", StringComparison.OrdinalIgnoreCase) || 
                fileName.Equals("global.html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = await File.ReadAllTextAsync(file, Encoding.UTF8);
            var (frontMatter, body) = FrontMatterParser.Split(text);

            // Parse the HTML body using ChtmlParser
            var bytes = Encoding.UTF8.GetBytes(body);
            var document = ChtmlParser.Parse(bytes);

            // Collect scripts from this file's AST
            var scripts = ScriptCollector.Collect(document);
            foreach (var script in scripts)
            {
                if (script.IsBottomScript)
                {
                    // Check if we already have this script content (deduplicate by content)
                    var existing = allScripts.FirstOrDefault(s => s.content == script.Content);
                    if (existing.id == null)
                    {
                        // New script - generate ID from content hash for consistency across compilations
                        var scriptId = script.ScriptId ?? ScriptCollector.GenerateId(script.Content);
                        script.ScriptId = scriptId;
                        allScripts.Add((scriptId, script.Content));
                    }
                }
            }
        }

        Console.WriteLine($"Collected {allScripts.Count} unique scripts from all files");
        return allScripts;
    }

    /// <summary>
    /// Collects all styles from template files, deduplicating by content.
    /// </summary>
    private static async Task<List<(string id, string content)>> CollectAllStyles(string[] files, string templatesRoot)
    {
        var allStyles = new List<(string id, string content)>();

        foreach (var file in files)
        {
            // Skip global.chtml in style collection
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("global.chtml", StringComparison.OrdinalIgnoreCase) || 
                fileName.Equals("global.html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = await File.ReadAllTextAsync(file, Encoding.UTF8);
            var (frontMatter, body) = FrontMatterParser.Split(text);

            // Parse the HTML body using ChtmlParser
            var bytes = Encoding.UTF8.GetBytes(body);
            var document = ChtmlParser.Parse(bytes);

            // Collect styles from this file's AST
            var styles = StyleCollector.Collect(document);
            foreach (var style in styles)
            {
                if (style.IsBottomStyle)
                {
                    // Check if we already have this style content (deduplicate by content)
                    var existing = allStyles.FirstOrDefault(s => s.content == style.Content);
                    if (existing.id == null)
                    {
                        // New style - generate ID from content hash for consistency across compilations
                        var styleId = style.StyleId ?? StyleCollector.GenerateId(style.Content);
                        style.StyleId = styleId;
                        allStyles.Add((styleId, style.Content));
                    }
                }
            }
        }

        Console.WriteLine($"Collected {allStyles.Count} unique styles from all files");
        return allStyles;
    }

    /// <summary>
    /// Determines the namespace for a component or page based on file path.
    /// Components use folder structure: components/header/stub -> Templates.Generated.Components.Header
    /// Pages use folder structure: pages/about/index -> Templates.Generated.Pages.About
    /// </summary>
    private static string DetermineNamespace(string file, bool isComponent, string relative)
    {
        var segments = relative.Split('/');
        string baseNamespace;
        int baseIndex;
        
        if (isComponent)
        {
            baseNamespace = "Templates.Generated.Components";
            baseIndex = -1;
            
            // Find "components" in path
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Equals("components", StringComparison.OrdinalIgnoreCase))
                {
                    baseIndex = i;
                    break;
                }
            }
            
            if (baseIndex == -1)
            {
                return baseNamespace;
            }
        }
        else
        {
            baseNamespace = "Templates.Generated.Pages";
            baseIndex = -1;
            
            // Find "pages" in path
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Equals("pages", StringComparison.OrdinalIgnoreCase))
                {
                    baseIndex = i;
                    break;
                }
            }
            
            if (baseIndex == -1)
            {
                return baseNamespace;
            }
        }
        
        var nsParts = new List<string> { "Templates", "Generated", isComponent ? "Components" : "Pages" };
        
        // Add folder names after "components" or "pages" up to (but not including) the file name
        for (int i = baseIndex + 1; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            // Remove bracket notation and clean up
            segment = RemoveBracketNotation(segment);
            var cleaned = System.Text.RegularExpressions.Regex.Replace(segment, "[^A-Za-z0-9]", "_");
            if (!string.IsNullOrEmpty(cleaned))
            {
                var pascalSegment = StringUtils.ToPascalCase(cleaned);
                // If starts with digit, prefix with underscore for valid C# namespace
                if (pascalSegment.Length > 0 && char.IsDigit(pascalSegment[0]))
                {
                    pascalSegment = "_" + pascalSegment;
                }
                nsParts.Add(pascalSegment);
            }
        }
        
        return string.Join(".", nsParts);
    }

    /// <summary>
    /// Determines the class name for a component or page based on the file path.
    /// For index files: always use "Index" (for both components and pages)
    /// For other files, uses the file name.
    /// </summary>
    private static string DetermineClassName(string file, bool isComponent, string relative)
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        fileName = RemoveBracketNotation(fileName);
        
        // For index files, always use "Index" (matching component pattern)
        if (fileName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return "Index";
        }
        
        // For other files, use the file name
        var cleanedFileName = System.Text.RegularExpressions.Regex.Replace(fileName, "[^A-Za-z0-9]", "_");
        return StringUtils.ToPascalCase(cleanedFileName);
    }

    /// <summary>
    /// Removes bracket notation from a segment name.
    /// [slug] -> slug, [...slugs] -> slugs
    /// </summary>
    private static string RemoveBracketNotation(string segment)
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

    /// <summary>
    /// Determines the route for a page based on file path convention.
    /// Routes are always convention-based, not from front matter.
    /// </summary>
    private static string? DetermineRoute(bool isComponent, Dictionary<string, object>? frontMatterDict, string relative)
    {
        if (isComponent)
            return null;

        // Routes are always convention-based from file path
        return RouteGenerator.GenerateFromPath(relative);
    }

    /// <summary>
    /// Merges route parameters with front matter props.
    /// Route parameters that aren't already defined in Props will be added as System.String props.
    /// </summary>
    private static Dictionary<string, object>? MergeRouteParametersWithProps(
        Dictionary<string, object>? frontMatterDict, 
        List<string> routeParameters, 
        string className)
    {
        if (routeParameters.Count == 0)
            return frontMatterDict;

        // Clone front matter dict or create new one
        var merged = frontMatterDict != null
            ? new Dictionary<string, object>(frontMatterDict)
            : new Dictionary<string, object>();

        // Get existing props if any
        var existingProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (merged.TryGetValue("Props", out var propsObj) && propsObj is Dictionary<string, object> propsDict)
        {
            foreach (var key in propsDict.Keys)
            {
                existingProps.Add(key);
            }
        }

        // Add route parameters that don't already exist as props
        bool hasNewParams = false;
        foreach (var param in routeParameters)
        {
            if (!existingProps.Contains(param))
            {
                hasNewParams = true;
                break;
            }
        }

        if (hasNewParams)
        {
            // Ensure Props dictionary exists
            if (!merged.ContainsKey("Props"))
            {
                merged["Props"] = new Dictionary<string, object>();
            }

            var propsDictToUpdate = merged["Props"] as Dictionary<string, object> 
                ?? new Dictionary<string, object>();
            merged["Props"] = propsDictToUpdate;

            // Add route parameters as System.String props
            foreach (var param in routeParameters)
            {
                if (!existingProps.Contains(param))
                {
                    propsDictToUpdate[param] = "System.String";
                }
            }
        }

        return merged;
    }

    /// <summary>
    /// Determines the input props type for route registration.
    /// </summary>
    private static string? DetermineInputPropsType(
        Dictionary<string, object>? frontMatterDict, 
        string className,
        Dictionary<string, object>? mergedProps = null)
    {
        // Use merged props if provided, otherwise use original front matter
        var propsToCheck = mergedProps ?? frontMatterDict;
        
        var hasInputProps = FrontMatterParser.ParseProps(propsToCheck, "Props").Any();
        var hasComputedProps = FrontMatterParser.ParseProps(propsToCheck, "ComputedProps").Any();

        // Registration uses TInputProps (what callers pass), not TProps (internal)
        // If computed props exist but no input props, use EmptyPropsInstance (null)
        // If computed props exist and input props exist, use InputProps
        // If only input props exist, use Props (same as InputProps)
        // If no props, use EmptyPropsInstance (null)
        if (hasComputedProps)
        {
            return hasInputProps ? $"{className}InputProps" : null;
        }
        else if (hasInputProps)
        {
            return $"{className}Props";
        }

        return null;
    }
}


