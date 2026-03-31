// Provides Test Data for the simulator test project support code.
using System;
using System.IO;

namespace Ct3xxSimulator.Tests;

internal static class TestData
{
    private static readonly Lazy<string> RootDirectoryLazy = new(ResolveRootDirectory);

    /// <summary>
    /// Gets the root directory.
    /// </summary>
    public static string RootDirectory => RootDirectoryLazy.Value;

    /// <summary>
    /// Gets the path.
    /// </summary>
    public static string GetPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException($"Unable to locate '{relativePath}'.", fullPath);
        }

        return fullPath;
    }

    /// <summary>
    /// Executes ResolveRootDirectory.
    /// </summary>
    private static string ResolveRootDirectory()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(current, "CT3xx.sln")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
            {
                break;
            }

            current = parent.FullName;
        }

        throw new InvalidOperationException("The repository root could not be located for simulator tests.");
    }
}
