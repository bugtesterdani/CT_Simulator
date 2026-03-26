using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ct3xxSimulator.Desktop.ViewModels;

public sealed class StepTreeNodeViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;

    public StepTreeNodeViewModel(string title, bool isGroup, StepTreeNodeViewModel? parent = null)
    {
        Title = title;
        IsGroup = isGroup;
        Parent = parent;
        Children = new ObservableCollection<StepTreeNodeViewModel>();
        _isExpanded = isGroup;
    }

    public string Title { get; }
    public bool IsGroup { get; }
    public StepTreeNodeViewModel? Parent { get; }
    public ObservableCollection<StepTreeNodeViewModel> Children { get; }
    public string? GroupMode { get; set; }
    public string? GroupHint { get; set; }
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
    public int ExpectedEvaluationCount { get; set; } = 1;
    public int ActualEvaluationCount { get; set; }
    public StepResultViewModel? Result { get; private set; }

    public string Outcome => Result?.Outcome ?? ComputeAggregateOutcome();
    public string MeasuredValue => Result?.MeasuredValue ?? string.Empty;
    public string LowerLimit => Result?.LowerLimit ?? string.Empty;
    public string UpperLimit => Result?.UpperLimit ?? string.Empty;
    public string Unit => Result?.Unit ?? string.Empty;
    public string Details => Result?.Details ?? string.Empty;
    public bool HasTraceView => Result != null && Result.Traces.Count > 0;
    public string NodeTypeLabel => BuildNodeTypeLabel();
    public string SummaryLine => IsGroup ? BuildGroupSummary() : BuildTestSummary();
    public bool HasDetailLine => !string.IsNullOrWhiteSpace(DetailLine);
    public string DetailLine => IsGroup ? (GroupHint ?? string.Empty) : Details;

    public void ApplyResult(StepResultViewModel result)
    {
        Result = result;
        ActualEvaluationCount++;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Outcome));
        OnPropertyChanged(nameof(MeasuredValue));
        OnPropertyChanged(nameof(LowerLimit));
        OnPropertyChanged(nameof(UpperLimit));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(HasTraceView));
        OnPropertyChanged(nameof(NodeTypeLabel));
        OnPropertyChanged(nameof(SummaryLine));
        OnPropertyChanged(nameof(HasDetailLine));
        OnPropertyChanged(nameof(DetailLine));
    }

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

    private string BuildMeasurementSummary()
    {
        var parts = new System.Collections.Generic.List<string>();

        if (!string.IsNullOrWhiteSpace(MeasuredValue))
        {
            parts.Add($"Ist: {MeasuredValue}{AppendUnit(Unit)}");
        }

        if (!string.IsNullOrWhiteSpace(LowerLimit) || !string.IsNullOrWhiteSpace(UpperLimit))
        {
            var lower = string.IsNullOrWhiteSpace(LowerLimit) ? "-" : LowerLimit;
            var upper = string.IsNullOrWhiteSpace(UpperLimit) ? "-" : UpperLimit;
            parts.Add($"Grenzen: {lower} .. {upper}{AppendUnit(Unit)}");
        }

        return string.Join("   |   ", parts);
    }

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

    private static string AppendUnit(string unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
