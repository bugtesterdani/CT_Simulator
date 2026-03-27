// Provides Group Details View Model for the desktop application view model support.
using System.Collections.ObjectModel;
using System.Linq;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the group details view model.
/// </summary>
public class GroupDetailsViewModel
{
    /// <summary>
    /// Gets the execution condition.
    /// </summary>
    public string? ExecutionCondition { get; init; }
    /// <summary>
    /// Gets the repeat condition.
    /// </summary>
    public string? RepeatCondition { get; init; }
    /// <summary>
    /// Gets the exec mode.
    /// </summary>
    public string? ExecMode { get; init; }
    /// <summary>
    /// Gets the loop count.
    /// </summary>
    public string? LoopCount { get; init; }
    /// <summary>
    /// Gets the log loops.
    /// </summary>
    public string? LogLoops { get; init; }

    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<GroupTableSummaryViewModel> Tables { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the execution condition condition is met.
    /// </summary>
    public bool HasExecutionCondition => !string.IsNullOrWhiteSpace(ExecutionCondition);
    /// <summary>
    /// Gets a value indicating whether the repeat condition condition is met.
    /// </summary>
    public bool HasRepeatCondition => !string.IsNullOrWhiteSpace(RepeatCondition);
    /// <summary>
    /// Gets a value indicating whether the exec mode condition is met.
    /// </summary>
    public bool HasExecMode => !string.IsNullOrWhiteSpace(ExecMode);
    /// <summary>
    /// Gets a value indicating whether the loop count condition is met.
    /// </summary>
    public bool HasLoopCount => !string.IsNullOrWhiteSpace(LoopCount);
    /// <summary>
    /// Gets a value indicating whether the log loops condition is met.
    /// </summary>
    public bool HasLogLoops => !string.IsNullOrWhiteSpace(LogLoops);
    /// <summary>
    /// Gets a value indicating whether the tables condition is met.
    /// </summary>
    public bool HasTables => Tables.Any(t => t.HasContent);

    /// <summary>
    /// Gets the repeat description.
    /// </summary>
    public string RepeatDescription => HasRepeatCondition
        ? $"Wiederhole solange '{RepeatCondition}' TRUE ergibt."
        : "Keine Wiederholbedingung definiert.";
}

/// <summary>
/// Represents the group table summary view model.
/// </summary>
public class GroupTableSummaryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupTableSummaryViewModel"/> class.
    /// </summary>
    public GroupTableSummaryViewModel(string title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Tabelle" : title;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<GroupVariableEntryViewModel> Variables { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<GroupRecordEntryViewModel> Assignments { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the variables condition is met.
    /// </summary>
    public bool HasVariables => Variables.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the assignments condition is met.
    /// </summary>
    public bool HasAssignments => Assignments.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the content condition is met.
    /// </summary>
    public bool HasContent => HasVariables || HasAssignments;
}

/// <summary>
/// Represents the group variable entry view model.
/// </summary>
public class GroupVariableEntryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupVariableEntryViewModel"/> class.
    /// </summary>
    public GroupVariableEntryViewModel(string name, string? type, string? initial)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Variable" : name;
        Type = string.IsNullOrWhiteSpace(type) ? null : type;
        Initial = string.IsNullOrWhiteSpace(initial) ? null : initial;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the type.
    /// </summary>
    public string? Type { get; }
    /// <summary>
    /// Gets the initial.
    /// </summary>
    public string? Initial { get; }

    /// <summary>
    /// Gets the initial display.
    /// </summary>
    public string InitialDisplay => Initial ?? "-";
}

/// <summary>
/// Represents the group record entry view model.
/// </summary>
public class GroupRecordEntryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupRecordEntryViewModel"/> class.
    /// </summary>
    public GroupRecordEntryViewModel(string title, string? destination, string? expression)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Zuweisung" : title;
        Destination = string.IsNullOrWhiteSpace(destination) ? null : destination;
        Expression = string.IsNullOrWhiteSpace(expression) ? null : expression;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the destination.
    /// </summary>
    public string? Destination { get; }
    /// <summary>
    /// Gets the expression.
    /// </summary>
    public string? Expression { get; }

    /// <summary>
    /// Gets the summary.
    /// </summary>
    public string Summary => Destination != null
        ? $"{Destination} := {Expression ?? "-"}"
        : Expression ?? "-";
}

public static class GroupDetailsFactory
{
    /// <summary>
    /// Executes create.
    /// </summary>
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
