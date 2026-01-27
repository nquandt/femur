using System;
using System.Diagnostics;
using System.IO;

namespace ChtmlCompiler;

/// <summary>
/// Shiki syntax highlighter for use during compilation.
/// Processes code blocks at compile time and embeds highlighted HTML in generated code.
/// </summary>
public static class ShikiProcessor
{
    private static string? _scriptPath;
    private static string _nodePath = "node";
    private static string _defaultTheme = "nord";
    private static int _timeoutMs = 5000;

    /// <summary>
    /// Initializes the Shiki processor with script path and options.
    /// Should be called once at the start of compilation.
    /// </summary>
    public static void Initialize(string? scriptPath = null, string nodePath = "node", string defaultTheme = "nord", int timeoutMs = 5000)
    {
        _scriptPath = scriptPath ?? FindShikiScript();
        _nodePath = nodePath;
        _defaultTheme = defaultTheme;
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Highlights code using Shiki and returns HTML.
    /// Returns null if Shiki is not available or fails.
    /// </summary>
    public static string? HighlightCode(string code, string language = "text", string? theme = null)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        if (_scriptPath == null || !File.Exists(_scriptPath))
        {
            Console.WriteLine($"[ShikiProcessor] Script not found at: {_scriptPath}");
            return null; // Shiki not available
        }

        theme ??= _defaultTheme;
        language = language?.ToLowerInvariant() ?? "text";

        try
        {
            var scriptDir = Path.GetDirectoryName(_scriptPath) ?? Directory.GetCurrentDirectory();
            var projectRoot = FindProjectRoot(scriptDir);
            var nodeModulesPath = projectRoot != null ? Path.Combine(projectRoot, "node_modules") : null;

            if (nodeModulesPath == null || !Directory.Exists(nodeModulesPath))
            {
                Console.WriteLine($"[ShikiProcessor] node_modules not found. Project root: {projectRoot}, node_modules: {nodeModulesPath}");
                return null;
            }

            var shikiPath = Path.Combine(nodeModulesPath, "shiki");
            if (!Directory.Exists(shikiPath))
            {
                Console.WriteLine($"[ShikiProcessor] Shiki module not found in node_modules at: {shikiPath}");
                return null;
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = _nodePath,
                Arguments = $"\"{_scriptPath}\" \"{EscapeArgument(code)}\" \"{language}\" \"{theme}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = scriptDir
            };

            // Set NODE_PATH to help find shiki module if node_modules exists
            if (nodeModulesPath != null && Directory.Exists(nodeModulesPath))
            {
                var existingPath = Environment.GetEnvironmentVariable("NODE_PATH") ?? "";
                processInfo.EnvironmentVariables["NODE_PATH"] =
                    string.IsNullOrEmpty(existingPath) ? nodeModulesPath : $"{nodeModulesPath}{Path.PathSeparator}{existingPath}";
            }

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(_timeoutMs))
            {
                process.Kill();
                Console.WriteLine($"[ShikiProcessor] Process timed out after {_timeoutMs}ms");
                return null; // Timeout
            }

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"[ShikiProcessor] Process failed with exit code {process.ExitCode}. Error: {error}");
                return null; // Shiki failed
            }

            return output.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShikiProcessor] Exception: {ex.Message}");
            return null; // Shiki not available or failed
        }
    }

    private static string? FindShikiScript()
    {
        // Look for shiki-transform.js in common locations
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = FindProjectRoot(currentDir);
        
        if (projectRoot != null)
        {
            // Check in project root
            var scriptPath = Path.Combine(projectRoot, "shiki-transform.js");
            if (File.Exists(scriptPath))
                return scriptPath;

            // Check in examples/nquandtcom
            var nquandtcomPath = Path.Combine(projectRoot, "examples", "nquandtcom", "shiki-transform.js");
            if (File.Exists(nquandtcomPath))
                return nquandtcomPath;
        }

        return null;
    }

    private static string? FindProjectRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "package.json")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string EscapeArgument(string arg)
    {
        return arg
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}

