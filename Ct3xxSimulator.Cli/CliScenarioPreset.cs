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
    /// <summary>
    /// Gets the installed tester cards by card name.
    /// </summary>
    public Dictionary<string, int> InstalledCards { get; set; } = new();
    /// <summary>
    /// Gets the test type to card mapping.
    /// Use "PC" for test types that do not require a tester card.
    /// </summary>
    public Dictionary<string, string> TestTypeCards { get; set; } = new();
    /// <summary>
    /// Gets the optional test type card rules.
    /// </summary>
    public List<Ct3xxSimulator.Validation.TestTypeCardRule> TestTypeCardRules { get; set; } = new();
    /// <summary>
    /// Gets the optional card index regex per card name.
    /// </summary>
    public Dictionary<string, string> CardIndexPatterns { get; set; } = new();
}
