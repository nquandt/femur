namespace HtmlParserTests;

/// <summary>
/// Base test fixture that ensures proper cleanup between tests.
/// Forces garbage collection to release parser resources.
/// </summary>
public class TestFixture : IDisposable
{
    public TestFixture()
    {
        // Force GC before test starts to clean up any lingering resources
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public void Dispose()
    {
        // Force GC after test completes to ensure resources are released
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

