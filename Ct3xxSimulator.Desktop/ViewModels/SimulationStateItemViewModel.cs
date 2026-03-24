namespace Ct3xxSimulator.Desktop.ViewModels;

public sealed class SimulationStateItemViewModel
{
    public SimulationStateItemViewModel(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string Value { get; }
}
