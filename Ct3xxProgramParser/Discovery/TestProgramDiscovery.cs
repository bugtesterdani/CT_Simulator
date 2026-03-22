using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ct3xxProgramParser.Discovery;

public sealed class TestProgramInfo
{
    public TestProgramInfo(string rootDirectory, string filePath)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        FilePath = Path.GetFullPath(filePath);
        FileName = Path.GetFileName(FilePath) ?? FilePath;

        var parentDirectory = Path.GetDirectoryName(FilePath) ?? RootDirectory;
        var relative = GetRelativePath(RootDirectory, parentDirectory);
        if (string.IsNullOrWhiteSpace(relative) || relative == "." || relative == Path.GetFileName(RootDirectory))
        {
            relative = Path.GetFileName(RootDirectory) ?? string.Empty;
        }

        RelativeDirectory = NormalizeSeparators(relative);
        DisplayName = string.IsNullOrWhiteSpace(RelativeDirectory)
            ? FileName
            : $"{RelativeDirectory} - {FileName}";
    }

    public string RootDirectory { get; }
    public string FilePath { get; }
    public string FileName { get; }
    public string RelativeDirectory { get; }
    public string DisplayName { get; }

    private static string NormalizeSeparators(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim('\\');

    private static string GetRelativePath(string basePath, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return targetPath;
        }

        var baseUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(basePath)));
        var targetUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(targetPath)));
        var relative = baseUri.MakeRelativeUri(targetUri).ToString();
        return relative.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}

public static class TestProgramDiscovery
{
    private const string DefaultFolderName = "testprogramme";

    public static string? FindRoot(string? startDirectory = null, int maxDepth = 8)
    {
        var fromEnv = Environment.GetEnvironmentVariable("CT3XX_TESTPROGRAM_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        var current = Path.GetFullPath(string.IsNullOrWhiteSpace(startDirectory)
            ? AppContext.BaseDirectory
            : startDirectory);

        for (var depth = 0; depth <= maxDepth && !string.IsNullOrWhiteSpace(current); depth++)
        {
            var candidate = Path.Combine(current, DefaultFolderName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
            {
                break;
            }

            current = parent.FullName;
        }

        return null;
    }

    public static IReadOnlyList<TestProgramInfo> EnumeratePrograms(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return Array.Empty<TestProgramInfo>();
        }

        var normalizedRoot = Path.GetFullPath(rootDirectory);
        var files = Directory.GetFiles(normalizedRoot, "*.ctxprg", SearchOption.AllDirectories);
        var infos = files
            .Select(file => new TestProgramInfo(normalizedRoot, file))
            .OrderBy(info => info.RelativeDirectory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(info => info.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return infos;
    }
}
