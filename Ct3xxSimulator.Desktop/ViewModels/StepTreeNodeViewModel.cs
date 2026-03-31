// Provides Step Tree Node View Model for the desktop application view model support.
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the step tree node view model.
/// </summary>
public sealed class StepTreeNodeViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _hasBreakpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepTreeNodeViewModel"/> class.
    /// </summary>
    public StepTreeNodeViewModel(string title, bool isGroup, string nodeKey, StepTreeNodeViewModel? parent = null)
    {
        Title = title;
        IsGroup = isGroup;
        NodeKey = nodeKey;
        Parent = parent;
        Children = new ObservableCollection<StepTreeNodeViewModel>();
        _isExpanded = isGroup;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets a value indicating whether the group condition is met.
    /// </summary>
    public bool IsGroup { get; }
    /// <summary>
    /// Gets the node key.
    /// </summary>
    public string NodeKey { get; }
    /// <summary>
    /// Gets the parent.
    /// </summary>
    public StepTreeNodeViewModel? Parent { get; }
    /// <summary>
    /// Gets the children.
    /// </summary>
    public ObservableCollection<StepTreeNodeViewModel> Children { get; }
    /// <summary>
    /// Gets the group mode.
    /// </summary>
    public string? GroupMode { get; set; }
    /// <summary>
    /// Gets the group hint.
    /// </summary>
    public string? GroupHint { get; set; }
    /// <summary>
    /// Gets the keep expanded.
    /// </summary>
    public bool KeepExpanded { get; set; }
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }
    /// <summary>
    /// Gets the expected evaluation count.
    /// </summary>
    public int ExpectedEvaluationCount { get; set; } = 1;
    /// <summary>
    /// Gets the actual evaluation count.
    /// </summary>
    public int ActualEvaluationCount { get; set; }
    /// <summary>
    /// Gets the result.
    /// </summary>
    public StepResultViewModel? Result { get; private set; }
    public bool HasBreakpoint
    {
        get => _hasBreakpoint;
        set
        {
            if (_hasBreakpoint == value)
            {
                return;
            }

            _hasBreakpoint = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BreakpointText));
        }
    }
    /// <summary>
    /// Gets the breakpoint text.
    /// </summary>
    public string BreakpointText => HasBreakpoint ? "BP" : string.Empty;
    /// <summary>
    /// Gets the result source label.
    /// </summary>
    public string ResultSourceLabel => Result?.ResultSourceLabel ?? string.Empty;
    /// <summary>
    /// Gets a value indicating whether the result source label should be shown.
    /// </summary>
    public bool HasResultSourceLabel => !string.IsNullOrWhiteSpace(ResultSourceLabel);
    /// <summary>
    /// Gets the status badge background color.
    /// </summary>
    public string OutcomeBadgeBackground => Result?.OutcomeBadgeBackground ?? "#FFEAF1F7";
    /// <summary>
    /// Gets the status badge foreground color.
    /// </summary>
    public string OutcomeBadgeForeground => Result?.OutcomeBadgeForeground ?? "#FF2E5E86";

    /// <summary>
    /// Executes compute aggregate outcome.
    /// </summary>
    public string Outcome => Result?.DisplayOutcome ?? ComputeAggregateOutcome();
    /// <summary>
    /// Gets the measured value.
    /// </summary>
    public string MeasuredValue => Result?.DisplayMeasuredValue ?? string.Empty;
    /// <summary>
    /// Gets the lower limit.
    /// </summary>
    public string LowerLimit => Result?.DisplayLowerLimit ?? string.Empty;
    /// <summary>
    /// Gets the upper limit.
    /// </summary>
    public string UpperLimit => Result?.DisplayUpperLimit ?? string.Empty;
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string Unit => Result?.DisplayUnit ?? string.Empty;
    /// <summary>
    /// Gets the details.
    /// </summary>
    public string Details => Result?.DisplayDetails ?? string.Empty;
    /// <summary>
    /// Gets a value indicating whether the trace view condition is met.
    /// </summary>
    public bool HasTraceView => Result != null && Result.Traces.Count > 0;
    /// <summary>
    /// Builds the node type label.
    /// </summary>
    public string NodeTypeLabel => BuildNodeTypeLabel();
    /// <summary>
    /// Builds the test summary.
    /// </summary>
    public string SummaryLine => IsGroup ? BuildGroupSummary() : BuildTestSummary();
    /// <summary>
    /// Builds the value summary.
    /// </summary>
    public string ValueSummary => IsGroup ? BuildGroupSummary() : BuildValueSummary();
    /// <summary>
    /// Builds the range summary.
    /// </summary>
    public string RangeSummary => IsGroup ? BuildGroupRangeSummary() : BuildRangeSummary();
    /// <summary>
    /// Gets a value indicating whether the detail line condition is met.
    /// </summary>
    public bool HasDetailLine => !string.IsNullOrWhiteSpace(DetailLine);
    /// <summary>
    /// Gets the detail line.
    /// </summary>
    public string DetailLine => IsGroup ? (GroupHint ?? string.Empty) : BuildDetailLine();

    /// <summary>
    /// Executes apply result.
    /// </summary>
    public void ApplyResult(StepResultViewModel result)
    {
        Result = result;
        ActualEvaluationCount++;
    }

    /// <summary>
    /// Executes refresh.
    /// </summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Outcome));
        OnPropertyChanged(nameof(MeasuredValue));
        OnPropertyChanged(nameof(LowerLimit));
        OnPropertyChanged(nameof(UpperLimit));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(HasTraceView));
        OnPropertyChanged(nameof(HasBreakpoint));
        OnPropertyChanged(nameof(BreakpointText));
        OnPropertyChanged(nameof(ResultSourceLabel));
        OnPropertyChanged(nameof(HasResultSourceLabel));
        OnPropertyChanged(nameof(OutcomeBadgeBackground));
        OnPropertyChanged(nameof(OutcomeBadgeForeground));
        OnPropertyChanged(nameof(NodeTypeLabel));
        OnPropertyChanged(nameof(SummaryLine));
        OnPropertyChanged(nameof(ValueSummary));
        OnPropertyChanged(nameof(RangeSummary));
        OnPropertyChanged(nameof(HasDetailLine));
        OnPropertyChanged(nameof(DetailLine));
    }

    /// <summary>
    /// Executes BuildNodeTypeLabel.
    /// </summary>
    private string BuildNodeTypeLabel()
    {
        if (!IsGroup)
        {
            return "Test";
        }

        if (KeepExpanded)
        {
            return "Test Loop";
        }

        if (!string.IsNullOrWhiteSpace(GroupMode))
        {
            return GroupMode!;
        }

        return "Gruppe";
    }

    /// <summary>
    /// Executes BuildGroupSummary.
    /// </summary>
    private string BuildGroupSummary()
    {
        var parts = new System.Collections.Generic.List<string>();

        parts.Add($"{Children.Count} Eintraege");

        if (!string.IsNullOrWhiteSpace(GroupMode) && !KeepExpanded)
        {
            parts.Add(GroupMode!);
        }

        return string.Join("   |   ", parts);
    }

    /// <summary>
    /// Executes BuildMeasurementSummary.
    /// </summary>
    private string BuildMeasurementSummary()
    {
        var parts = new System.Collections.Generic.List<string>();

        var valueSummary = BuildValueSummary();
        if (!string.IsNullOrWhiteSpace(valueSummary))
        {
            parts.Add(valueSummary);
        }

        var rangeSummary = BuildRangeSummary();
        if (!string.IsNullOrWhiteSpace(rangeSummary))
        {
            parts.Add(rangeSummary);
        }

        return string.Join("   |   ", parts);
    }

    /// <summary>
    /// Executes BuildDetailLine.
    /// </summary>
    private string BuildDetailLine()
    {
        if (Result?.HasComparisonSummary == true)
        {
            return Result.ComparisonSummary;
        }

        return Details;
    }

    /// <summary>
    /// Executes BuildTestSummary.
    /// </summary>
    private string BuildTestSummary()
    {
        if (Children.Count > 0)
        {
            var parts = new System.Collections.Generic.List<string> { $"{Children.Count} Untereintraege" };
            var measurement = BuildMeasurementSummary();
            if (!string.IsNullOrWhiteSpace(measurement))
            {
                parts.Add(measurement);
            }

            return string.Join("   |   ", parts);
        }

        return BuildMeasurementSummary();
    }

    /// <summary>
    /// Executes BuildValueSummary.
    /// </summary>
    private string BuildValueSummary()
    {
        if (string.IsNullOrWhiteSpace(MeasuredValue))
        {
            return string.Empty;
        }

        return $"{MeasuredValue}{AppendUnit(Unit)}";
    }

    /// <summary>
    /// Executes BuildRangeSummary.
    /// </summary>
    private string BuildRangeSummary()
    {
        if (string.IsNullOrWhiteSpace(LowerLimit) && string.IsNullOrWhiteSpace(UpperLimit))
        {
            return string.Empty;
        }

        var lower = string.IsNullOrWhiteSpace(LowerLimit) ? "-" : LowerLimit;
        var upper = string.IsNullOrWhiteSpace(UpperLimit) ? "-" : UpperLimit;
        return $"{lower} .. {upper}{AppendUnit(Unit)}";
    }

    /// <summary>
    /// Executes BuildGroupRangeSummary.
    /// </summary>
    private string BuildGroupRangeSummary()
    {
        if (string.IsNullOrWhiteSpace(GroupHint))
        {
            return string.Empty;
        }

        var firstSentence = GroupHint!.Split(new[] { '.', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return firstSentence ?? GroupHint!;
    }

    /// <summary>
    /// Executes AppendUnit.
    /// </summary>
    private static string AppendUnit(string unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
    }

    /// <summary>
    /// Executes ComputeAggregateOutcome.
    /// </summary>
    private string ComputeAggregateOutcome()
    {
        if (Children.Count == 0)
        {
            return string.Empty;
        }

        var outcomes = Children
            .Select(child => child.Outcome)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (outcomes.Count == 0)
        {
            return string.Empty;
        }

        if (outcomes.Any(value => value.Equals("ERROR", System.StringComparison.OrdinalIgnoreCase)))
        {
            return "ERROR";
        }

        if (outcomes.Any(value => value.Equals("FAIL", System.StringComparison.OrdinalIgnoreCase)))
        {
            return "FAIL";
        }

        if (outcomes.All(value => value.Equals("PASS", System.StringComparison.OrdinalIgnoreCase)))
        {
            return "PASS";
        }

        return string.Empty;
    }

    /// <summary>
    /// Occurs when PropertyChanged is raised.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Executes OnPropertyChanged.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
