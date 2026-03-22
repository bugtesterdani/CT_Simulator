using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.FileParsers;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Parsing;

namespace Ct3xxProgramParser.Programs;

public sealed class Ct3xxProgramFileParser
{
    private readonly Ct3xxProgramLoader _programLoader;
    private readonly Ct3xxFileParserRegistry _registry;

    public Ct3xxProgramFileParser(
        Ct3xxProgramLoader? programLoader = null,
        Ct3xxFileParserRegistry? registry = null)
    {
        _programLoader = programLoader ?? new Ct3xxProgramLoader();
        _registry = registry ?? Ct3xxFileParserRegistry.CreateDefault();
    }

    public Ct3xxProgramFileSet Load(string programFilePath, bool includeExternalFiles = true)
    {
        if (string.IsNullOrWhiteSpace(programFilePath))
        {
            throw new ArgumentException("Program file path must be provided.", nameof(programFilePath));
        }

        var program = _programLoader.Load(programFilePath);
        var externalFiles = includeExternalFiles
            ? LoadExternalFiles(programFilePath, program)
            : Array.Empty<Ct3xxFileDocument>();

        return new Ct3xxProgramFileSet(programFilePath, program, externalFiles);
    }

    private IReadOnlyList<Ct3xxFileDocument> LoadExternalFiles(string programFilePath, Ct3xxProgram program)
    {
        var programDirectory = Path.GetDirectoryName(Path.GetFullPath(programFilePath)) ?? Directory.GetCurrentDirectory();
        var documents = new List<Ct3xxFileDocument>();
        var tables = EnumerateTables(program).ToList();

        foreach (var table in tables)
        {
            var relative = NormalizePath(table.File);
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(programDirectory, relative));
            var parser = _registry.FindParser(fullPath);
            if (parser == null)
            {
                continue;
            }

            try
            {
                documents.Add(parser.Parse(fullPath, table));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse '{fullPath}' with parser '{parser.GetType().Name}'.", ex);
            }
        }

        return documents;
    }

    private static IEnumerable<Table> EnumerateTables(Ct3xxProgram program)
    {
        var visited = new HashSet<Table>();

        foreach (var table in EnumerateTables(program.Tables, visited))
        {
            yield return table;
        }

        foreach (var table in EnumerateSequenceTables(program.RootItems, visited))
        {
            yield return table;
        }

        if (program.DutLoop?.Items != null)
        {
            foreach (var table in EnumerateSequenceTables(program.DutLoop.Items, visited))
            {
                yield return table;
            }
        }

        if (program.Application?.Tables != null)
        {
            foreach (var table in EnumerateTables(program.Application.Tables, visited))
            {
                yield return table;
            }
        }
    }

    private static IEnumerable<Table> EnumerateTables(IEnumerable<Table>? tables, HashSet<Table> visited)
    {
        if (tables == null)
        {
            yield break;
        }

        foreach (var table in tables)
        {
            foreach (var descendant in EnumerateTableAndChildren(table, visited))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<Table> EnumerateTableAndChildren(Table? table, HashSet<Table> visited)
    {
        if (table == null)
        {
            yield break;
        }

        if (!visited.Add(table))
        {
            yield break;
        }

        yield return table;

        if (table.Libraries == null)
        {
            yield break;
        }

        foreach (var library in table.Libraries)
        {
            foreach (var function in library.Functions)
            {
                foreach (var nested in EnumerateTables(function.Tables, visited))
                {
                    yield return nested;
                }

                foreach (var node in function.Items)
                {
                    foreach (var nested in EnumerateSequenceTables(node, visited))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }

    private static IEnumerable<Table> EnumerateSequenceTables(IEnumerable<SequenceNode>? nodes, HashSet<Table> visited)
    {
        if (nodes == null)
        {
            yield break;
        }

        foreach (var node in nodes)
        {
            foreach (var table in EnumerateSequenceTables(node, visited))
            {
                yield return table;
            }
        }
    }

    private static IEnumerable<Table> EnumerateSequenceTables(SequenceNode? node, HashSet<Table> visited)
    {
        if (node == null)
        {
            yield break;
        }

        switch (node)
        {
            case Table tableNode:
                foreach (var descendant in EnumerateTableAndChildren(tableNode, visited))
                {
                    yield return descendant;
                }
                break;
            case Group group:
                foreach (var child in EnumerateSequenceTables(group.Items, visited))
                {
                    yield return child;
                }
                break;
            case Test test:
                foreach (var table in EnumerateTables(test.Parameters?.Tables, visited))
                {
                    yield return table;
                }
                break;
        }
    }

    private static string? NormalizePath(string? path)
    {
        var candidate = path;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var nonNull = candidate!;
        var trimmed = nonNull.Trim().Trim('\'', '"');
        if (trimmed.IndexOf('(') >= 0 && trimmed.IndexOf(')') > trimmed.IndexOf('('))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
