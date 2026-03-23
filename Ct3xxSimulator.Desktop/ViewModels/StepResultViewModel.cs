namespace Ct3xxSimulator.Desktop.ViewModels;

public sealed class StepResultViewModel
{
    public StepResultViewModel(
        string stepName,
        string outcome,
        string measuredValue,
        string lowerLimit,
        string upperLimit,
        string unit,
        string details)
    {
        StepName = stepName;
        Outcome = outcome;
        MeasuredValue = measuredValue;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        Unit = unit;
        Details = details;
    }

    public string StepName { get; }
    public string Outcome { get; }
    public string MeasuredValue { get; }
    public string LowerLimit { get; }
    public string UpperLimit { get; }
    public string Unit { get; }
    public string Details { get; }
}
