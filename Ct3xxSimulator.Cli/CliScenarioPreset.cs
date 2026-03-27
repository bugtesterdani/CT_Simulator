// Provides Cli Scenario Preset for the command-line interface support code.
namespace Ct3xxSimulator.Cli;

internal sealed class CliScenarioPreset
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Gets the test program folder path.
    /// </summary>
    public string? TestProgramFolderPath { get; set; }
    /// <summary>
    /// Gets the wiring folder path.
    /// </summary>
    public string? WiringFolderPath { get; set; }
    /// <summary>
    /// Gets the simulation model folder path.
    /// </summary>
    public string? SimulationModelFolderPath { get; set; }
    /// <summary>
    /// Gets the python script path.
    /// </summary>
    public string? PythonScriptPath { get; set; }
}
