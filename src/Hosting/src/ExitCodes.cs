namespace Femur.Hosting;

/// <summary>
/// Standard exit codes and associated error messages used throughout the Femur.Hosting framework.
/// These exit codes provide consistent error reporting across console and web applications.
/// Each exit code is paired with its corresponding log message for better developer understanding.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Normal successful completion. The application completed without errors.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Builder creation failed. An error occurred during the initial setup or configuration
    /// of the application builder before the build process could begin.
    /// </summary>
    public const int BuilderCreationFailed = 1;

    /// <summary>
    /// Application build failed. An error occurred during the application build process,
    /// typically when configuring services or building the dependency injection container.
    /// </summary>
    public const int BuildFailed = 2;

    /// <summary>
    /// General runtime error. An unhandled exception occurred during application execution
    /// that was not specifically categorized as a pre-startup or post-shutdown error.
    /// </summary>
    public const int RuntimeError = 3;

    /// <summary>
    /// Pre-startup error. An error occurred during the application startup phase,
    /// typically during app.RunAsync() before the application has fully started.
    /// </summary>
    public const int PreStartupError = 4;

    /// <summary>
    /// Post-shutdown error. An error occurred during the application shutdown phase,
    /// after the main application logic has completed but during cleanup operations.
    /// </summary>
    public const int PostShutdownError = 5;

    /// <summary>
    /// Bootstrap logger creation failed. An error occurred during the early initialization
    /// phase when setting up the bootstrap logging system, resulting in early exit.
    /// </summary>
    public const int BootstrapLoggerFailed = 10;

    /// <summary>
    /// Command cancelled by user request. The application was cancelled through normal
    /// user interaction (e.g., cancellation token triggered by user action).
    /// </summary>
    public const int CommandCancelled = 125;

    /// <summary>
    /// SIGINT received (Ctrl+C). The application was interrupted by a SIGINT signal,
    /// typically when the user presses Ctrl+C to terminate the application.
    /// </summary>
    public const int CtrlCInterrupt = 130;

    // Test-specific exit codes (used in unit tests)

    /// <summary>
    /// Custom test exit code. Used in unit tests to verify specific exit code scenarios.
    /// This is not used in production code.
    /// </summary>
    public const int TestCustom42 = 42;

    /// <summary>
    /// Custom test exit code. Used in unit tests to verify specific exit code scenarios.
    /// This is not used in production code.
    /// </summary>
    public const int TestCustom77 = 77;

    /// <summary>
    /// Standard error messages paired with exit codes for consistent logging.
    /// Use these messages when logging errors that result in specific exit codes.
    /// </summary>
    public static class Messages
    {
        /// <summary>
        /// Success completion message (Exit Code: <see cref="Success"/>).
        /// </summary>
        public const string SuccessfulCompletion = "Application has shut down gracefully";

        /// <summary>
        /// Builder creation failure message (Exit Code: <see cref="BuilderCreationFailed"/>).
        /// For console applications: "Console application host builder creation failed"
        /// For web applications: "Web application builder creation failed"
        /// </summary>
        public const string BuilderCreationFailed = "{0} application host builder creation failed";
        public const string ConsoleBuilderCreationFailed = "Console application host builder creation failed";
        public const string WebBuilderCreationFailed = "Web application builder creation failed";

        /// <summary>
        /// Application build failure message (Exit Code: <see cref="BuildFailed"/>).
        /// For console applications: "Console application host build failed"
        /// For web applications: "Web application build failed"
        /// </summary>
        public const string BuildFailed = "{0} application build failed";
        public const string ConsoleBuildFailed = "Console application host build failed";
        public const string WebBuildFailed = "Web application build failed";

        /// <summary>
        /// Runtime error message (Exit Code: <see cref="RuntimeError"/>).
        /// For console applications: "Console application runtime error"
        /// For web applications: "Web application runtime error"
        /// </summary>
        public const string RuntimeError = "{0} application runtime error";
        public const string ConsoleRuntimeError = "Console application runtime error";
        public const string WebRuntimeError = "Web application runtime error";

        /// <summary>
        /// Pre-startup error message (Exit Code: <see cref="PreStartupError"/>).
        /// For console applications: "Console application pre-startup error"
        /// For web applications: "Web application pre-startup error"
        /// </summary>
        public const string PreStartupError = "{0} application pre-startup error";
        public const string ConsolePreStartupError = "Console application pre-startup error";
        public const string WebPreStartupError = "Web application pre-startup error";

        /// <summary>
        /// Post-shutdown error message (Exit Code: <see cref="PostShutdownError"/>).
        /// For console applications: "Console application post-shutdown error"
        /// For web applications: "Web application post-shutdown error"
        /// </summary>
        public const string PostShutdownError = "{0} application post-shutdown error";
        public const string ConsolePostShutdownError = "Console application post-shutdown error";
        public const string WebPostShutdownError = "Web application post-shutdown error";

        /// <summary>
        /// Bootstrap logger failure message (Exit Code: <see cref="BootstrapLoggerFailed"/>).
        /// </summary>
        public const string BootstrapLoggerFailed = "Bootstrap logger creation failed - exiting early";

        /// <summary>
        /// Build configuration error message (Exit Code: <see cref="BuildFailed"/>).
        /// Used when configuration phase fails during build.
        /// </summary>
        public const string BuildConfigurationError = "{0} application build configuration error";
        public const string ConsoleBuildConfigurationError = "Console application build error";
        public const string WebBuildConfigurationError = "Web application build failed";

        /// <summary>
        /// Cancellation messages (Exit Codes: <see cref="CommandCancelled"/>, <see cref="CtrlCInterrupt"/>).
        /// </summary>
        public const string CommandCancelled = "Console application was cancelled, exit code: {ExitCode}";
        public const string CtrlCInterrupt = "Console application was cancelled via stopping token, exit code: {ExitCode}";
        public const string ApplicationExecutionError = "An error occurred during console application execution, setting exit code to {ExitCode}";
    }

    /// <summary>
    /// Provides structured information about exit codes, including code, message, and description.
    /// </summary>
    public static class Info
    {
        /// <summary>
        /// Gets information about a specific exit code.
        /// </summary>
        /// <param name="exitCode">The exit code to get information for.</param>
        /// <returns>Exit code information including description and typical scenarios.</returns>
        public static ExitCodeInfo GetExitCodeInfo(int exitCode)
        {
            return exitCode switch
            {
                Success => new ExitCodeInfo(Success, "Success", "Normal successful completion", "Application completed without errors"),
                BuilderCreationFailed => new ExitCodeInfo(BuilderCreationFailed, "Builder Creation Failed", "Builder setup error", "Error during initial application builder setup or configuration"),
                BuildFailed => new ExitCodeInfo(BuildFailed, "Build Failed", "Application build error", "Error during application build process, typically when configuring services or DI container"),
                RuntimeError => new ExitCodeInfo(RuntimeError, "Runtime Error", "General runtime error", "Unhandled exception during application execution"),
                PreStartupError => new ExitCodeInfo(PreStartupError, "Pre-Startup Error", "Startup phase error", "Error during application startup phase, typically during RunAsync() before full startup"),
                PostShutdownError => new ExitCodeInfo(PostShutdownError, "Post-Shutdown Error", "Shutdown phase error", "Error during application shutdown phase, after main logic completed during cleanup"),
                BootstrapLoggerFailed => new ExitCodeInfo(BootstrapLoggerFailed, "Bootstrap Logger Failed", "Early logging error", "Error during early initialization when setting up bootstrap logging system"),
                CommandCancelled => new ExitCodeInfo(CommandCancelled, "Command Cancelled", "User cancellation", "Application cancelled through normal user interaction or cancellation token"),
                CtrlCInterrupt => new ExitCodeInfo(CtrlCInterrupt, "Ctrl+C Interrupt", "SIGINT received", "Application interrupted by SIGINT signal, typically when user presses Ctrl+C"),
                TestCustom42 => new ExitCodeInfo(TestCustom42, "Test Custom 42", "Test exit code", "Custom exit code used in unit tests (not for production)"),
                TestCustom77 => new ExitCodeInfo(TestCustom77, "Test Custom 77", "Test exit code", "Custom exit code used in unit tests (not for production)"),
                _ => new ExitCodeInfo(exitCode, "Unknown", "Unknown exit code", "Exit code not defined in the standard Femur.Hosting framework")
            };
        }

        /// <summary>
        /// Gets all standard exit codes with their information.
        /// </summary>
        /// <returns>Dictionary of exit codes and their information.</returns>
        public static Dictionary<int, ExitCodeInfo> GetAllExitCodes()
        {
            return new Dictionary<int, ExitCodeInfo>
            {
                { Success, GetExitCodeInfo(Success) },
                { BuilderCreationFailed, GetExitCodeInfo(BuilderCreationFailed) },
                { BuildFailed, GetExitCodeInfo(BuildFailed) },
                { RuntimeError, GetExitCodeInfo(RuntimeError) },
                { PreStartupError, GetExitCodeInfo(PreStartupError) },
                { PostShutdownError, GetExitCodeInfo(PostShutdownError) },
                { BootstrapLoggerFailed, GetExitCodeInfo(BootstrapLoggerFailed) },
                { CommandCancelled, GetExitCodeInfo(CommandCancelled) },
                { CtrlCInterrupt, GetExitCodeInfo(CtrlCInterrupt) },
                { TestCustom42, GetExitCodeInfo(TestCustom42) },
                { TestCustom77, GetExitCodeInfo(TestCustom77) }
            };
        }
    }
}

/// <summary>
/// Contains detailed information about an exit code.
/// </summary>
/// <param name="Code">The numeric exit code value.</param>
/// <param name="Name">The friendly name of the exit code.</param>
/// <param name="Category">The category or type of error.</param>
/// <param name="Description">Detailed description of when this exit code is used.</param>
public record ExitCodeInfo(int Code, string Name, string Category, string Description);