// Provides Test Data for the program parser test project support code.
using System;
using System.IO;
using Ct3xxProgramParser.Discovery;

namespace Ct3xxProgramParser.Tests;

internal static class TestData
{
    private static readonly Lazy<string> _root = new(ResolveRoot);

    /// <summary>
    /// Gets the test program root.
    /// </summary>
    public static string TestProgramRoot => _root.Value;

    /// <summary>
    /// Gets the program file path.
    /// </summary>
    public static string GetProgramFilePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
        }

        var combine = Path.Combine(TestProgramRoot, relativePath);
        if (!File.Exists(combine))
        {
            throw new FileNotFoundException($"Unable to locate sample file '{relativePath}'.", combine);
        }

        return combine;
    }

    private static string ResolveRoot()
    {
        var root = TestProgramDiscovery.FindRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("The 'testprogramme' folder could not be located for integration tests.");
        }

        return root;
    }
}
