// Provides Simulation State Item View Model for the desktop application view model support.
namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the simulation state item view model.
/// </summary>
public sealed class SimulationStateItemViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationStateItemViewModel"/> class.
    /// </summary>
    public SimulationStateItemViewModel(string name, string value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }
}
