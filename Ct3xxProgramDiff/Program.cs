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

        var addedKeys = new HashSet<string>(newNodes.Keys.Except(oldNodes.Keys), StringComparer.Ordinal);
        var removedKeys = new HashSet<string>(oldNodes.Keys.Except(newNodes.Keys), StringComparer.Ordinal);
        var added = addedKeys.Select(k => newNodes[k]).ToList();
        var removed = removedKeys.Select(k => oldNodes[k]).ToList();
        var changed = new List<ChangeEntry>();
        var changedMap = new Dictionary<string, ChangeEntry>(StringComparer.Ordinal);

        foreach (var key in oldNodes.Keys.Intersect(newNodes.Keys))
        {
            var before = oldNodes[key];
            var after = newNodes[key];
            var diffs = DiffFields(before, after);
            if (diffs.Count > 0)
            {
                var entry = new ChangeEntry(before, after, diffs);
                changed.Add(entry);
                changedMap[key] = entry;
            }
        }

        var oldDisplayRows = BuildDisplayRows(oldProgram);
        var newDisplayRows = BuildDisplayRows(newProgram);

        var report = BuildMarkdownReport(oldPath, newPath, added, removed, changed);
        File.WriteAllText(outPath, report, Encoding.UTF8);

        var html = BuildHtmlReport(oldPath, newPath, oldDisplayRows, newDisplayRows);
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

        if (program.DutLoop != null)
        {
            nodes.Add(NodeInfo.FromDutLoop(program.DutLoop, "/DUTLoop"));
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

    private static List<DisplayRow> BuildDisplayRows(Ct3xxProgram program)
    {
        var rows = new List<DisplayRow>();

        if (program.Tables != null && program.Tables.Count > 0)
        {
            var tablePath = "/Tables";
            foreach (var table in program.Tables)
            {
                var node = NodeInfo.FromTable(table, tablePath);
                rows.Add(new DisplayRow(node, 0, NodeIndexer.BuildKey(node), BuildMatchKey(node)));
            }
        }

        WalkDisplayRows(rows, program.RootItems, "/Program", 0);

        if (program.DutLoop != null)
        {
            var loopNode = NodeInfo.FromDutLoop(program.DutLoop, "/DUTLoop");
            rows.Add(new DisplayRow(loopNode, 0, NodeIndexer.BuildKey(loopNode), BuildMatchKey(loopNode)));
            WalkDisplayRows(rows, program.DutLoop.Items, "/DUTLoop", 1);
        }

        if (program.Application?.Tables != null && program.Application.Tables.Count > 0)
        {
            var appPath = "/ApplicationTables";
            foreach (var table in program.Application.Tables)
            {
                var node = NodeInfo.FromTable(table, appPath);
                rows.Add(new DisplayRow(node, 0, NodeIndexer.BuildKey(node), BuildMatchKey(node)));
            }
        }

        return rows;
    }

    private static void WalkDisplayRows(List<DisplayRow> rows, IEnumerable<SequenceNode>? items, string parentPath, int depth)
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
                    var groupNode = NodeInfo.FromGroup(group, groupPath);
                    rows.Add(new DisplayRow(groupNode, depth, NodeIndexer.BuildKey(groupNode), BuildMatchKey(groupNode)));
                    WalkDisplayRows(rows, group.Items, groupPath, depth + 1);
                    break;
                case Test test:
                    var testPath = $"{parentPath}/Test[{index}:{test.Name ?? test.File ?? "unnamed"}]";
                    var testNode = NodeInfo.FromTest(test, testPath);
                    rows.Add(new DisplayRow(testNode, depth, NodeIndexer.BuildKey(testNode), BuildMatchKey(testNode)));
                    WalkDisplayRows(rows, test.Items, testPath, depth + 1);
                    if (test.Parameters?.Tables != null)
                    {
                        foreach (var table in test.Parameters.Tables)
                        {
                            var tableNode = NodeInfo.FromTable(table, $"{testPath}/Tables");
                            rows.Add(new DisplayRow(tableNode, depth + 1, NodeIndexer.BuildKey(tableNode), BuildMatchKey(tableNode)));
                        }
                    }
                    break;
                case Table table:
                    var tablePath = $"{parentPath}/Table[{index}:{table.File ?? table.Id ?? "unnamed"}]";
                    var tableInfo = NodeInfo.FromTable(table, tablePath);
                    rows.Add(new DisplayRow(tableInfo, depth, NodeIndexer.BuildKey(tableInfo), BuildMatchKey(tableInfo)));
                    break;
            }

            index++;
        }
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
        AddDiff(diffs, "Limits", before.LimitDetails, after.LimitDetails);
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
        List<DisplayRow> oldRows,
        List<DisplayRow> newRows)
    {
        var alignedRows = AlignRows(oldRows, newRows);
        var addedCount = alignedRows.Count(row => row.Status == DiffStatus.Added);
        var removedCount = alignedRows.Count(row => row.Status == DiffStatus.Removed);
        var changedCount = alignedRows.Count(row => row.Status == DiffStatus.Changed);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\"/>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.AppendLine("<title>CTXPRG Semantic Diff</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(":root{--border:#D7DEE7;--card:#FDFEFE;--group:#F6F8FB;--text:#293241;--muted:#6B7480;--badge:#E2E8EF;--added:#CBE5D2;--removed:#F1C1C1;--changed:#F9E1A5;}");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;background:#F4F7FA;color:#222;}");
        sb.AppendLine("h1{margin-top:0;} section{margin-top:24px;}");
        sb.AppendLine(".meta{color:#555;font-size:14px;}");
        sb.AppendLine(".count{margin:12px 0;font-weight:600;}");
        sb.AppendLine(".panel{background:white;border:1px solid var(--border);border-radius:8px;padding:12px;}");
        sb.AppendLine(".panel-header{display:flex;align-items:center;gap:12px;margin-bottom:10px;}");
        sb.AppendLine(".panel-title{font-size:18px;font-weight:700;color:var(--text);}");
        sb.AppendLine(".columns{display:grid;grid-template-columns:2.5fr 1.4fr 2.5fr;gap:12px;margin-bottom:8px;font-weight:700;color:#3B4652;}");
        sb.AppendLine(".row{display:grid;grid-template-columns:2.5fr 1.4fr 2.5fr;gap:12px;margin-bottom:8px;align-items:start;}");
        sb.AppendLine(".node{background:var(--card);border:1px solid #E2E8EF;border-radius:6px;padding:8px;position:relative;}");
        sb.AppendLine(".node.group{background:var(--group);border-color:#D9E2EC;}");
        sb.AppendLine(".node .title{font-weight:600;color:var(--text);} .node .meta{font-size:11px;color:var(--muted);margin-top:3px;}");
        sb.AppendLine(".badge{display:inline-block;padding:4px 8px;border-radius:10px;font-weight:700;border:1px solid var(--badge);background:#F5F7FA;color:#3B4652;font-size:12px;}");
        sb.AppendLine(".badge.added{background:#EAF7EF;border-color:var(--added);color:#2E7D32;}");
        sb.AppendLine(".badge.removed{background:#FDEDEE;border-color:var(--removed);color:#8B1E3F;}");
        sb.AppendLine(".badge.changed{background:#FFF6E0;border-color:var(--changed);color:#915E00;}");
        sb.AppendLine(".diffs{font-size:12px;color:#3B4652;line-height:1.4;}");
        sb.AppendLine(".diffs div{margin-bottom:6px;}");
        sb.AppendLine(".diffs .label{font-weight:700;color:#324152;}");
        sb.AppendLine(".diffs .old{color:#8B1E3F;}");
        sb.AppendLine(".diffs .new{color:#2E7D32;}");
        sb.AppendLine(".empty{min-height:40px;}");
        sb.AppendLine(".indent{display:block;}");
        sb.AppendLine(".collapsed-block{margin:6px 0 12px 0;}");
        sb.AppendLine(".toggle-unchanged{background:#EEF2F6;border:1px dashed #C4CFDA;color:#3B4652;border-radius:6px;padding:6px 10px;font-size:12px;font-weight:600;cursor:pointer;}");
        sb.AppendLine(".toggle-unchanged:focus{outline:2px solid #7AA2C2;outline-offset:2px;}");
        sb.AppendLine(".collapsed-rows{display:none;margin-top:8px;}");
        sb.AppendLine(".collapsed-block.open .collapsed-rows{display:block;}");
        sb.AppendLine(".collapsed-block.open .toggle-unchanged{background:#E8F1FF;border-style:solid;}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<h1>CTXPRG Diff (CT Style)</h1>");
        sb.AppendLine($"<div class=\"meta\">Old: <code>{Escape(Path.GetFullPath(oldPath))}</code></div>");
        sb.AppendLine($"<div class=\"meta\">New: <code>{Escape(Path.GetFullPath(newPath))}</code></div>");
        sb.AppendLine($"<div class=\"count\">Added: {addedCount} &nbsp; Removed: {removedCount} &nbsp; Changed: {changedCount}</div>");

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"panel-header\"><div class=\"panel-title\">Testschritte</div></div>");
        sb.AppendLine("<div class=\"columns\"><div>Alt</div><div>Diff / Status</div><div>Neu</div></div>");
        sb.AppendLine(RenderAlignedRows(alignedRows));
        sb.AppendLine("</section>");

        sb.AppendLine("<script>");
        sb.AppendLine("document.querySelectorAll('.toggle-unchanged').forEach(btn => {");
        sb.AppendLine("  const showText = btn.dataset.showText || 'Show unchanged';");
        sb.AppendLine("  const hideText = btn.dataset.hideText || 'Hide unchanged';");
        sb.AppendLine("  btn.textContent = showText;");
        sb.AppendLine("  btn.addEventListener('click', () => {");
        sb.AppendLine("    const block = btn.closest('.collapsed-block');");
        sb.AppendLine("    if (!block) return;");
        sb.AppendLine("    const open = block.classList.toggle('open');");
        sb.AppendLine("    btn.textContent = open ? hideText : showText;");
        sb.AppendLine("  });");
        sb.AppendLine("});");
        sb.AppendLine("</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string RenderSideBySideRow(AlignedRow row)
    {
        var leftCell = row.OldRow != null ? RenderNodeCell(row.OldRow) : "<div class=\"empty\"></div>";
        var rightCell = row.NewRow != null ? RenderNodeCell(row.NewRow) : "<div class=\"empty\"></div>";
        var middleCell = RenderDiffCell(row);

        return $@"
<div class=""row"">
  <div>{leftCell}</div>
  <div class=""diffs"">{middleCell}</div>
  <div>{rightCell}</div>
</div>";
    }

    private static string RenderAlignedRows(List<AlignedRow> rows)
    {
        var changedSubtree = ComputeChangedSubtreeFlags(rows);
        var sb = new StringBuilder();
        var i = 0;
        var groupId = 0;

        while (i < rows.Count)
        {
            var isCollapsible = rows[i].Status == DiffStatus.Unchanged && !changedSubtree[i];
            if (!isCollapsible)
            {
                sb.AppendLine(RenderSideBySideRow(rows[i]));
                i++;
                continue;
            }

            var start = i;
            while (i < rows.Count && rows[i].Status == DiffStatus.Unchanged && !changedSubtree[i])
            {
                i++;
            }

            var count = i - start;
            sb.AppendLine(RenderCollapsedBlock(rows, start, count, groupId++));
        }

        return sb.ToString();
    }

    private static string RenderCollapsedBlock(List<AlignedRow> rows, int start, int count, int groupId)
    {
        var showText = $"Show {count} unchanged row{(count == 1 ? string.Empty : "s")}";
        var hideText = $"Hide {count} unchanged row{(count == 1 ? string.Empty : "s")}";
        var sb = new StringBuilder();

        sb.AppendLine($"<div class=\"collapsed-block\" id=\"unchanged-{groupId}\">");
        sb.AppendLine($"  <button type=\"button\" class=\"toggle-unchanged\" data-show-text=\"{Escape(showText)}\" data-hide-text=\"{Escape(hideText)}\"></button>");
        sb.AppendLine("  <div class=\"collapsed-rows\">");
        for (var i = start; i < start + count; i++)
        {
            sb.AppendLine(RenderSideBySideRow(rows[i]));
        }
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        return sb.ToString();
    }

    private static bool[] ComputeChangedSubtreeFlags(List<AlignedRow> rows)
    {
        var result = new bool[rows.Count];
        var stack = new Stack<(int Depth, bool HasChanged)>();

        for (var i = rows.Count - 1; i >= 0; i--)
        {
            var depth = GetRowDepth(rows[i]);
            var hasChanged = rows[i].Status != DiffStatus.Unchanged;

            while (stack.Count > 0 && stack.Peek().Depth > depth)
            {
                hasChanged |= stack.Pop().HasChanged;
            }

            result[i] = hasChanged;
            stack.Push((depth, hasChanged));
        }

        return result;
    }

    private static int GetRowDepth(AlignedRow row)
        => row.OldRow?.Depth ?? row.NewRow?.Depth ?? 0;

    private static string RenderNodeCell(DisplayRow row)
    {
        var node = row.Node;
        var depth = row.Depth;
        var title = BuildNodeTitle(node);
        var meta = BuildNodeMeta(node);
        var nodeClass = node.Kind.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "node group" : "node";
        return $@"
<div class=""{nodeClass}"" style=""margin-left:{depth * 18}px;"">
  <div class=""title"">{Escape(title)}</div>
  <div class=""meta"">{Escape(meta)}</div>
</div>";
    }

    private static string RenderDiffCell(AlignedRow row)
    {
        var badgeClass = row.Status switch
        {
            DiffStatus.Added => "badge added",
            DiffStatus.Removed => "badge removed",
            DiffStatus.Changed => "badge changed",
            _ => "badge"
        };

        var statusText = row.Status switch
        {
            DiffStatus.Added => "Added",
            DiffStatus.Removed => "Removed",
            DiffStatus.Changed => "Changed",
            _ => "Unchanged"
        };

        var diffBody = row.Status switch
        {
            DiffStatus.Added when row.NewRow != null => BuildFullDetailsBlock("New", row.NewRow.Node),
            DiffStatus.Removed when row.OldRow != null => BuildFullDetailsBlock("Old", row.OldRow.Node),
            DiffStatus.Changed when row.Change != null => string.Join(string.Empty, row.Change.Diffs.Select(diff =>
                $"<div><span class=\"label\">{Escape(diff.Field)}</span><br/>" +
                $"<span class=\"old\">Old:</span> <code>{Escape(diff.OldValue)}</code><br/>" +
                $"<span class=\"new\">New:</span> <code>{Escape(diff.NewValue)}</code></div>")),
            _ => string.Empty
        };

        return $@"
<div><span class=""{badgeClass}"">{Escape(statusText)}</span></div>
{diffBody}";
    }

    private static string BuildNodeTitle(NodeInfo node)
    {
        if (node.Kind.Equals("Group", StringComparison.OrdinalIgnoreCase))
        {
            return node.Name ?? node.Id ?? "Group";
        }

        if (node.Kind.Equals("Loop", StringComparison.OrdinalIgnoreCase))
        {
            return node.Name ?? "Test Loop";
        }

        if (node.Kind.Equals("Test", StringComparison.OrdinalIgnoreCase))
        {
            return node.ParameterName ?? node.Name ?? node.Id ?? "Test";
        }

        return node.File ?? node.Id ?? "Table";
    }

    private static string BuildNodeMeta(NodeInfo node)
    {
        var label = node.Kind;
        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            label += $" ({node.Id})";
        }

        if (!string.IsNullOrWhiteSpace(node.File))
        {
            label += $" | File={node.File}";
        }

        return label;
    }

    private static string BuildFullDetailsBlock(string label, NodeInfo node)
    {
        var parts = new List<(string Label, string? Value)>
        {
            ("Id", node.Id),
            ("Name", node.Name),
            ("File", node.File),
            ("LogFlags", node.LogFlags),
            ("Disabled", node.Disabled),
            ("Split", node.Split),
            ("Param.Name", node.ParameterName),
            ("Param.Library", node.ParameterLibrary),
            ("Param.Function", node.ParameterFunction),
            ("Param.Mode", node.ParameterMode),
            ("Param.Options", node.ParameterOptions),
            ("Param.Message", node.ParameterMessage),
            ("DrawingRef", node.DrawingReference),
            ("Limits", node.LimitDetails)
        };

        var lines = parts
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"<div><span class=\"label\">{Escape(item.Label)}</span><br/><span class=\"new\">{Escape(label)}:</span> <code>{Escape(item.Value ?? string.Empty)}</code></div>");

        return string.Join(string.Empty, lines);
    }

    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static List<AlignedRow> AlignRows(List<DisplayRow> oldRows, List<DisplayRow> newRows)
    {
        var oldKeys = oldRows.Select(row => row.MatchKey).ToArray();
        var newKeys = newRows.Select(row => row.MatchKey).ToArray();
        var dp = BuildLcsMatrix(oldKeys, newKeys);
        var aligned = new List<AlignedRow>();

        var i = 0;
        var j = 0;
        while (i < oldKeys.Length && j < newKeys.Length)
        {
            if (string.Equals(oldKeys[i], newKeys[j], StringComparison.Ordinal))
            {
                var oldRow = oldRows[i];
                var newRow = newRows[j];
                var diffs = DiffFields(oldRow.Node, newRow.Node);
                var status = diffs.Count > 0 ? DiffStatus.Changed : DiffStatus.Unchanged;
                var change = diffs.Count > 0 ? new ChangeEntry(oldRow.Node, newRow.Node, diffs) : null;
                aligned.Add(new AlignedRow(oldRow, newRow, status, change));
                i++;
                j++;
                continue;
            }

            if (dp[i + 1, j] >= dp[i, j + 1])
            {
                aligned.Add(new AlignedRow(oldRows[i], null, DiffStatus.Removed, null));
                i++;
            }
            else
            {
                aligned.Add(new AlignedRow(null, newRows[j], DiffStatus.Added, null));
                j++;
            }
        }

        while (i < oldKeys.Length)
        {
            aligned.Add(new AlignedRow(oldRows[i], null, DiffStatus.Removed, null));
            i++;
        }

        while (j < newKeys.Length)
        {
            aligned.Add(new AlignedRow(null, newRows[j], DiffStatus.Added, null));
            j++;
        }

        return aligned;
    }

    private static int[,] BuildLcsMatrix(string[] oldKeys, string[] newKeys)
    {
        var dp = new int[oldKeys.Length + 1, newKeys.Length + 1];
        for (var i = oldKeys.Length - 1; i >= 0; i--)
        {
            for (var j = newKeys.Length - 1; j >= 0; j--)
            {
                dp[i, j] = string.Equals(oldKeys[i], newKeys[j], StringComparison.Ordinal)
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        return dp;
    }

    private static string BuildMatchKey(NodeInfo node)
    {
        var normalizedPath = NormalizePath(node.Path);
        return $"{node.Kind}:{normalizedPath}";
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var sb = new StringBuilder(path.Length);
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] != '[')
            {
                sb.Append(path[i]);
                i++;
                continue;
            }

            var close = path.IndexOf(']', i + 1);
            if (close == -1)
            {
                sb.Append(path[i]);
                i++;
                continue;
            }

            var colon = path.IndexOf(':', i + 1);
            if (colon > -1 && colon < close)
            {
                sb.Append('[');
                sb.Append(path, colon + 1, close - colon - 1);
                sb.Append(']');
                i = close + 1;
                continue;
            }

            sb.Append(path, i, close - i + 1);
            i = close + 1;
        }

        return sb.ToString();
    }
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
    string? DrawingReference,
    string? LimitDetails)
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
            null,
            null);

    /// <summary>
    /// Creates a node info from a DUT loop.
    /// </summary>
    public static NodeInfo FromDutLoop(DutLoop loop, string path)
        => new(
            "Loop",
            path,
            loop.Id,
            loop.Name ?? "Test Loop",
            null,
            null,
            loop.Disabled,
            null,
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
            test.Parameters?.DrawingReference,
            BuildLimitDetails(test));

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
            null,
            BuildTableLimitDetails(table));

    private static string? BuildLimitDetails(Test test)
    {
        if (test.Parameters == null && (test.Debug == null || test.Debug.Count == 0))
        {
            return null;
        }

        var parts = new List<string>();

        var parameters = test.Parameters;
        if (parameters != null)
        {
            AddLimitAttribute(parts, "Options", parameters.Options);
            AddLimitAttribute(parts, "Mode", parameters.Mode);
            AddLimitAttribute(parts, "Function", parameters.Function);
            AddLimitAttribute(parts, "Library", parameters.Library);
            AddLimitAttribute(parts, "Name", parameters.Name);

            if (parameters.AdditionalAttributes != null)
            {
                foreach (var attribute in parameters.AdditionalAttributes)
                {
                    if (attribute == null)
                    {
                        continue;
                    }

                    if (IsLimitName(attribute.Name))
                    {
                        parts.Add($"{attribute.Name}={attribute.Value}");
                    }
                }
            }

            if (parameters.AcquisitionChannel1 != null)
            {
                AddChannelLimits(parts, "Acq1", parameters.AcquisitionChannel1);
            }

            if (parameters.AcquisitionChannel2 != null)
            {
                AddChannelLimits(parts, "Acq2", parameters.AcquisitionChannel2);
            }

            if (parameters.AcquisitionChannel3 != null)
            {
                AddChannelLimits(parts, "Acq3", parameters.AcquisitionChannel3);
            }

            if (parameters.StimulusChannel1 != null)
            {
                AddStimulusLimits(parts, "Sti1", parameters.StimulusChannel1);
            }

            if (parameters.StimulusChannel2 != null)
            {
                AddStimulusLimits(parts, "Sti2", parameters.StimulusChannel2);
            }

            if (parameters.Records != null)
            {
                foreach (var record in parameters.Records)
                {
                    AddRecordLimits(parts, record, prefix: "ParamRec");
                }
            }
        }

        if (test.Debug != null)
        {
            foreach (var debug in test.Debug)
            {
                if (!string.IsNullOrWhiteSpace(debug.RangeLowerLimit) ||
                    !string.IsNullOrWhiteSpace(debug.RangeUpperLimit) ||
                    !string.IsNullOrWhiteSpace(debug.LowerLimit) ||
                    !string.IsNullOrWhiteSpace(debug.UpperLimit))
                {
                    parts.Add($"Debug[{debug.Id ?? "-"}] RangeLower={debug.RangeLowerLimit} RangeUpper={debug.RangeUpperLimit} Lower={debug.LowerLimit} Upper={debug.UpperLimit}");
                }
            }
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" | ", parts);
    }

    private static string? BuildTableLimitDetails(Table table)
    {
        if (table.Records == null || table.Records.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var record in table.Records)
        {
            AddRecordLimits(parts, record, prefix: "Rec");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static void AddRecordLimits(List<string> parts, Record record, string prefix)
    {
        if (record == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(record.LowerLimit) &&
            string.IsNullOrWhiteSpace(record.UpperLimit) &&
            string.IsNullOrWhiteSpace(record.Unit) &&
            string.IsNullOrWhiteSpace(record.Voltage) &&
            string.IsNullOrWhiteSpace(record.Resistance))
        {
            return;
        }

        var id = record.Id ?? record.Index ?? record.Variable ?? record.Text ?? "-";
        parts.Add($"{prefix}[{id}] Lower={record.LowerLimit} Upper={record.UpperLimit} Unit={record.Unit} V={record.Voltage} R={record.Resistance}");
    }

    private static void AddChannelLimits(List<string> parts, string label, AcquisitionChannel channel)
    {
        if (channel == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(channel.LowerVoltageLimit) ||
            !string.IsNullOrWhiteSpace(channel.UpperVoltageLimit))
        {
            parts.Add($"{label}.VoltageLimits={channel.LowerVoltageLimit}..{channel.UpperVoltageLimit}");
        }

        if (channel.AdditionalAttributes != null)
        {
            foreach (var attribute in channel.AdditionalAttributes)
            {
                if (attribute == null)
                {
                    continue;
                }

                if (IsLimitName(attribute.Name))
                {
                    parts.Add($"{label}.{attribute.Name}={attribute.Value}");
                }
            }
        }
    }

    private static void AddStimulusLimits(List<string> parts, string label, StimulusChannel channel)
    {
        if (channel == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(channel.Voltage) ||
            !string.IsNullOrWhiteSpace(channel.Current) ||
            !string.IsNullOrWhiteSpace(channel.VoltageLimit))
        {
            parts.Add($"{label}.Stimulus V={channel.Voltage} I={channel.Current} VLimit={channel.VoltageLimit}");
        }
    }

    private static void AddLimitAttribute(List<string> parts, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (IsLimitName(label) || IsLimitName(value))
        {
            parts.Add($"{label}={value}");
        }
    }

    private static bool IsLimitName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.Trim();
        return normalized.Contains("Limit", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Lower", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Upper", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Min", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Max", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Range", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Tol", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Threshold", StringComparison.OrdinalIgnoreCase);
    }
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

    public static string BuildKey(NodeInfo node)
    {
        var idPart = node.Id ?? string.Empty;
        var namePart = node.Name ?? string.Empty;
        var filePart = node.File ?? string.Empty;
        return $"{node.Kind}:path:{node.Path}:{idPart}:{namePart}:{filePart}";
    }
}

internal sealed record DisplayRow(NodeInfo Node, int Depth, string Key, string MatchKey);

internal sealed record AlignedRow(DisplayRow? OldRow, DisplayRow? NewRow, DiffStatus Status, ChangeEntry? Change);

internal enum DiffStatus
{
    Unchanged,
    Added,
    Removed,
    Changed
}
