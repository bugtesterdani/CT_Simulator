using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;

/// <summary>
/// Provides a semantic CTXPRG diff CLI.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Executes the CTXPRG diff tool.
    /// </summary>
    /// <param name="args">The CLI arguments.</param>
    public static int Main(string[] args)
    {
        var oldPath = GetArg(args, "--old") ?? GetArg(args, "-o");
        var newPath = GetArg(args, "--new") ?? GetArg(args, "-n");
        var outPath = GetArg(args, "--out") ?? GetArg(args, "-r") ?? "ctxprg-diff.md";
        var htmlPath = GetArg(args, "--html") ?? GetArg(args, "-h") ?? "ctxprg-diff.html";

        if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
        {
            WriteUsage();
            return 2;
        }

        if (!File.Exists(oldPath) || !File.Exists(newPath))
        {
            Console.Error.WriteLine("ERROR: Both --old and --new must point to existing .ctxprg files.");
            return 3;
        }

        var parser = new Ct3xxProgramFileParser();
        var oldProgram = parser.Load(oldPath, includeExternalFiles: false).Program;
        var newProgram = parser.Load(newPath, includeExternalFiles: false).Program;

        var oldNodes = NodeIndexer.Index(BuildNodeList(oldProgram));
        var newNodes = NodeIndexer.Index(BuildNodeList(newProgram));

        var added = newNodes.Keys.Except(oldNodes.Keys).Select(k => newNodes[k]).ToList();
        var removed = oldNodes.Keys.Except(newNodes.Keys).Select(k => oldNodes[k]).ToList();
        var changed = new List<ChangeEntry>();

        foreach (var key in oldNodes.Keys.Intersect(newNodes.Keys))
        {
            var before = oldNodes[key];
            var after = newNodes[key];
            var diffs = DiffFields(before, after);
            if (diffs.Count > 0)
            {
                changed.Add(new ChangeEntry(before, after, diffs));
            }
        }

        var report = BuildMarkdownReport(oldPath, newPath, added, removed, changed);
        File.WriteAllText(outPath, report, Encoding.UTF8);

        var html = BuildHtmlReport(oldPath, newPath, added, removed, changed);
        File.WriteAllText(htmlPath, html, Encoding.UTF8);

        Console.WriteLine($"Report written to: {Path.GetFullPath(outPath)}");
        Console.WriteLine($"HTML written to:   {Path.GetFullPath(htmlPath)}");
        Console.WriteLine($"Added: {added.Count}  Removed: {removed.Count}  Changed: {changed.Count}");

        return 0;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  Ct3xxProgramDiff --old <old.ctxprg> --new <new.ctxprg> [--out report.md] [--html report.html]");
    }

    private static List<NodeInfo> BuildNodeList(Ct3xxProgram program)
    {
        var nodes = new List<NodeInfo>();

        if (program.Tables != null && program.Tables.Count > 0)
        {
            var tablePath = "/Tables";
            foreach (var table in program.Tables)
            {
                nodes.Add(NodeInfo.FromTable(table, tablePath));
            }
        }

        WalkSequence(nodes, program.RootItems, "/Program");

        if (program.DutLoop?.Items != null)
        {
            WalkSequence(nodes, program.DutLoop.Items, "/DUTLoop");
        }

        if (program.Application?.Tables != null && program.Application.Tables.Count > 0)
        {
            var appPath = "/ApplicationTables";
            foreach (var table in program.Application.Tables)
            {
                nodes.Add(NodeInfo.FromTable(table, appPath));
            }
        }

        return nodes;
    }

    private static void WalkSequence(List<NodeInfo> nodes, IEnumerable<SequenceNode>? items, string parentPath)
    {
        if (items == null)
        {
            return;
        }

        var index = 0;
        foreach (var node in items)
        {
            switch (node)
            {
                case Group group:
                    var groupPath = $"{parentPath}/Group[{index}:{group.Name ?? "unnamed"}]";
                    nodes.Add(NodeInfo.FromGroup(group, groupPath));
                    WalkSequence(nodes, group.Items, groupPath);
                    break;
                case Test test:
                    var testPath = $"{parentPath}/Test[{index}:{test.Name ?? test.File ?? "unnamed"}]";
                    nodes.Add(NodeInfo.FromTest(test, testPath));
                    WalkSequence(nodes, test.Items, testPath);
                    if (test.Parameters?.Tables != null)
                    {
                        foreach (var table in test.Parameters.Tables)
                        {
                            nodes.Add(NodeInfo.FromTable(table, $"{testPath}/Tables"));
                        }
                    }
                    break;
                case Table table:
                    var tablePath = $"{parentPath}/Table[{index}:{table.File ?? table.Id ?? "unnamed"}]";
                    nodes.Add(NodeInfo.FromTable(table, tablePath));
                    break;
            }

            index++;
        }
    }

    private static List<FieldDiff> DiffFields(NodeInfo before, NodeInfo after)
    {
        var diffs = new List<FieldDiff>();
        AddDiff(diffs, "Name", before.Name, after.Name);
        AddDiff(diffs, "File", before.File, after.File);
        AddDiff(diffs, "LogFlags", before.LogFlags, after.LogFlags);
        AddDiff(diffs, "Disabled", before.Disabled, after.Disabled);
        AddDiff(diffs, "Split", before.Split, after.Split);
        AddDiff(diffs, "Param.Name", before.ParameterName, after.ParameterName);
        AddDiff(diffs, "Param.Library", before.ParameterLibrary, after.ParameterLibrary);
        AddDiff(diffs, "Param.Function", before.ParameterFunction, after.ParameterFunction);
        AddDiff(diffs, "Param.Mode", before.ParameterMode, after.ParameterMode);
        AddDiff(diffs, "Param.Options", before.ParameterOptions, after.ParameterOptions);
        AddDiff(diffs, "Param.Message", before.ParameterMessage, after.ParameterMessage);
        AddDiff(diffs, "DrawingRef", before.DrawingReference, after.DrawingReference);
        return diffs;
    }

    private static void AddDiff(List<FieldDiff> diffs, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
        {
            diffs.Add(new FieldDiff(field, oldValue ?? string.Empty, newValue ?? string.Empty));
        }
    }

    private static string BuildMarkdownReport(
        string oldPath,
        string newPath,
        List<NodeInfo> added,
        List<NodeInfo> removed,
        List<ChangeEntry> changed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# CTXPRG Semantic Diff");
        sb.AppendLine();
        sb.AppendLine($"Old: `{Path.GetFullPath(oldPath)}`");
        sb.AppendLine($"New: `{Path.GetFullPath(newPath)}`");
        sb.AppendLine();
        sb.AppendLine($"Added: **{added.Count}**  Removed: **{removed.Count}**  Changed: **{changed.Count}**");
        sb.AppendLine();

        if (added.Count > 0)
        {
            sb.AppendLine("## Added (in new)");
            foreach (var item in added.OrderBy(i => i.Path))
            {
                sb.AppendLine($"- {item.Kind} `{item.Path}`");
            }
            sb.AppendLine();
        }

        if (removed.Count > 0)
        {
            sb.AppendLine("## Removed (missing in new)");
            foreach (var item in removed.OrderBy(i => i.Path))
            {
                sb.AppendLine($"- {item.Kind} `{item.Path}`");
            }
            sb.AppendLine();
        }

        if (changed.Count > 0)
        {
            sb.AppendLine("## Changed");
            foreach (var entry in changed.OrderBy(c => c.Before.Path))
            {
                sb.AppendLine($"- {entry.Before.Kind} `{entry.Before.Path}`");
                foreach (var diff in entry.Diffs)
                {
                    sb.AppendLine($"  - {diff.Field}: `{diff.OldValue}` -> `{diff.NewValue}`");
                }
            }
        }

        return sb.ToString();
    }

    private static string BuildHtmlReport(
        string oldPath,
        string newPath,
        List<NodeInfo> added,
        List<NodeInfo> removed,
        List<ChangeEntry> changed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\"/>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.AppendLine("<title>CTXPRG Semantic Diff</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;background:#fafafa;color:#222;}");
        sb.AppendLine("h1{margin-top:0;} section{margin-top:24px;}");
        sb.AppendLine(".meta{color:#555;font-size:14px;}");
        sb.AppendLine(".count{margin:12px 0;font-weight:600;}");
        sb.AppendLine(".added{background:#e6f7ea;border-left:4px solid #2e7d32;padding:6px 10px;margin:4px 0;}");
        sb.AppendLine(".removed{background:#fdecea;border-left:4px solid #c62828;padding:6px 10px;margin:4px 0;}");
        sb.AppendLine(".changed{background:#fff6e0;border-left:4px solid #f9a825;padding:6px 10px;margin:6px 0;}");
        sb.AppendLine("code{background:#f0f0f0;padding:2px 4px;border-radius:4px;}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<h1>CTXPRG Semantic Diff</h1>");
        sb.AppendLine($"<div class=\"meta\">Old: <code>{Escape(Path.GetFullPath(oldPath))}</code></div>");
        sb.AppendLine($"<div class=\"meta\">New: <code>{Escape(Path.GetFullPath(newPath))}</code></div>");
        sb.AppendLine($"<div class=\"count\">Added: {added.Count} &nbsp; Removed: {removed.Count} &nbsp; Changed: {changed.Count}</div>");

        if (added.Count > 0)
        {
            sb.AppendLine("<section><h2>Added (in new)</h2>");
            foreach (var item in added.OrderBy(i => i.Path))
            {
                sb.AppendLine($"<div class=\"added\"><strong>{Escape(item.Kind)}</strong> <code>{Escape(item.Path)}</code></div>");
            }
            sb.AppendLine("</section>");
        }

        if (removed.Count > 0)
        {
            sb.AppendLine("<section><h2>Removed (missing in new)</h2>");
            foreach (var item in removed.OrderBy(i => i.Path))
            {
                sb.AppendLine($"<div class=\"removed\"><strong>{Escape(item.Kind)}</strong> <code>{Escape(item.Path)}</code></div>");
            }
            sb.AppendLine("</section>");
        }

        if (changed.Count > 0)
        {
            sb.AppendLine("<section><h2>Changed</h2>");
            foreach (var entry in changed.OrderBy(c => c.Before.Path))
            {
                sb.AppendLine($"<div class=\"changed\"><div><strong>{Escape(entry.Before.Kind)}</strong> <code>{Escape(entry.Before.Path)}</code></div>");
                sb.AppendLine("<ul>");
                foreach (var diff in entry.Diffs)
                {
                    sb.AppendLine($"<li>{Escape(diff.Field)}: <code>{Escape(diff.OldValue)}</code> \u2192 <code>{Escape(diff.NewValue)}</code></li>");
                }
                sb.AppendLine("</ul></div>");
            }
            sb.AppendLine("</section>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

/// <summary>
/// Represents a captured program node for diffing.
/// </summary>
internal sealed record NodeInfo(
    string Kind,
    string Path,
    string? Id,
    string? Name,
    string? File,
    string? LogFlags,
    string? Disabled,
    string? Split,
    string? ParameterName,
    string? ParameterLibrary,
    string? ParameterFunction,
    string? ParameterMode,
    string? ParameterOptions,
    string? ParameterMessage,
    string? DrawingReference)
{
    /// <summary>
    /// Creates a node info from a group.
    /// </summary>
    public static NodeInfo FromGroup(Group group, string path)
        => new(
            "Group",
            path,
            group.Id,
            group.Name,
            null,
            null,
            group.Disabled,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    /// <summary>
    /// Creates a node info from a test.
    /// </summary>
    public static NodeInfo FromTest(Test test, string path)
        => new(
            "Test",
            path,
            test.Id,
            test.Name,
            test.File,
            test.LogFlags,
            test.Disabled,
            test.Split,
            test.Parameters?.Name,
            test.Parameters?.Library,
            test.Parameters?.Function,
            test.Parameters?.Mode,
            test.Parameters?.Options,
            test.Parameters?.Message,
            test.Parameters?.DrawingReference);

    /// <summary>
    /// Creates a node info from a table.
    /// </summary>
    public static NodeInfo FromTable(Table table, string path)
        => new(
            "Table",
            path,
            table.Id,
            null,
            table.File,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
}

/// <summary>
/// Represents a node diff field.
/// </summary>
internal sealed record FieldDiff(string Field, string OldValue, string NewValue);

/// <summary>
/// Represents a change entry.
/// </summary>
internal sealed record ChangeEntry(NodeInfo Before, NodeInfo After, List<FieldDiff> Diffs);

/// <summary>
/// Provides node indexing helpers.
/// </summary>
internal static class NodeIndexer
{
    /// <summary>
    /// Builds an indexed map for diffing.
    /// </summary>
    public static Dictionary<string, NodeInfo> Index(List<NodeInfo> nodes)
    {
        var map = new Dictionary<string, NodeInfo>(StringComparer.Ordinal);
        var collisionCounter = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            var key = BuildKey(node);
            if (map.ContainsKey(key))
            {
                var count = collisionCounter.TryGetValue(key, out var existing) ? existing + 1 : 1;
                collisionCounter[key] = count;
                key = $"{key}#{count}";
            }

            map[key] = node;
        }

        return map;
    }

    private static string BuildKey(NodeInfo node)
    {
        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            return $"{node.Kind}:id:{node.Id}";
        }

        var namePart = node.Name ?? string.Empty;
        var filePart = node.File ?? string.Empty;
        return $"{node.Kind}:path:{node.Path}:{namePart}:{filePart}";
    }
}
