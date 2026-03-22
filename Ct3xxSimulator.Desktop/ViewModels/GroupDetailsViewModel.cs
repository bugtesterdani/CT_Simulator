using System.Collections.ObjectModel;
using System.Linq;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Desktop.ViewModels;

public class GroupDetailsViewModel
{
    public string? ExecutionCondition { get; init; }
    public string? RepeatCondition { get; init; }
    public string? ExecMode { get; init; }
    public string? LoopCount { get; init; }
    public string? LogLoops { get; init; }

    public ObservableCollection<GroupTableSummaryViewModel> Tables { get; } = new();

    public bool HasExecutionCondition => !string.IsNullOrWhiteSpace(ExecutionCondition);
    public bool HasRepeatCondition => !string.IsNullOrWhiteSpace(RepeatCondition);
    public bool HasExecMode => !string.IsNullOrWhiteSpace(ExecMode);
    public bool HasLoopCount => !string.IsNullOrWhiteSpace(LoopCount);
    public bool HasLogLoops => !string.IsNullOrWhiteSpace(LogLoops);
    public bool HasTables => Tables.Any(t => t.HasContent);

    public string RepeatDescription => HasRepeatCondition
        ? $"Wiederhole solange '{RepeatCondition}' TRUE ergibt."
        : "Keine Wiederholbedingung definiert.";
}

public class GroupTableSummaryViewModel
{
    public GroupTableSummaryViewModel(string title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Tabelle" : title;
    }

    public string Title { get; }
    public ObservableCollection<GroupVariableEntryViewModel> Variables { get; } = new();
    public ObservableCollection<GroupRecordEntryViewModel> Assignments { get; } = new();

    public bool HasVariables => Variables.Count > 0;
    public bool HasAssignments => Assignments.Count > 0;
    public bool HasContent => HasVariables || HasAssignments;
}

public class GroupVariableEntryViewModel
{
    public GroupVariableEntryViewModel(string name, string? type, string? initial)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Variable" : name;
        Type = string.IsNullOrWhiteSpace(type) ? null : type;
        Initial = string.IsNullOrWhiteSpace(initial) ? null : initial;
    }

    public string Name { get; }
    public string? Type { get; }
    public string? Initial { get; }

    public string InitialDisplay => Initial ?? "-";
}

public class GroupRecordEntryViewModel
{
    public GroupRecordEntryViewModel(string title, string? destination, string? expression)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Zuweisung" : title;
        Destination = string.IsNullOrWhiteSpace(destination) ? null : destination;
        Expression = string.IsNullOrWhiteSpace(expression) ? null : expression;
    }

    public string Title { get; }
    public string? Destination { get; }
    public string? Expression { get; }

    public string Summary => Destination != null
        ? $"{Destination} := {Expression ?? "-"}"
        : Expression ?? "-";
}

public static class GroupDetailsFactory
{
    public static GroupDetailsViewModel Create(Group group)
    {
        var viewModel = new GroupDetailsViewModel
        {
            ExecutionCondition = TestDetailsFactory.Clean(group.ExecCondition),
            RepeatCondition = TestDetailsFactory.Clean(group.RepeatCondition),
            ExecMode = TestDetailsFactory.Clean(group.ExecMode),
            LoopCount = TestDetailsFactory.Clean(group.LoopCount),
            LogLoops = TestDetailsFactory.Clean(group.LogLoops)
        };

        foreach (var table in group.Items.OfType<Table>())
        {
            var tableVm = new GroupTableSummaryViewModel(TestDetailsFactory.Clean(table.Id) ?? "Tabelle");

            foreach (var variable in table.Variables)
            {
                var variableVm = new GroupVariableEntryViewModel(
                    TestDetailsFactory.Clean(variable.Name) ?? variable.Id ?? "Variable",
                    TestDetailsFactory.Clean(variable.Type),
                    TestDetailsFactory.Clean(variable.Initial));
                tableVm.Variables.Add(variableVm);
            }

            foreach (var record in table.Records)
            {
                var destination = TestDetailsFactory.Clean(record.Destination);
                var expression = TestDetailsFactory.Clean(record.Expression);
                if (string.IsNullOrWhiteSpace(destination) && string.IsNullOrWhiteSpace(expression))
                {
                    continue;
                }

                var title = TestDetailsFactory.Clean(record.Text)
                            ?? TestDetailsFactory.Clean(record.DrawingReference)
                            ?? destination
                            ?? record.Id
                            ?? "Zuweisung";

                tableVm.Assignments.Add(new GroupRecordEntryViewModel(title, destination, expression));
            }

            if (tableVm.HasContent)
            {
                viewModel.Tables.Add(tableVm);
            }
        }

        return viewModel;
    }
}
