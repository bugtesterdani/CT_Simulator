namespace Ct3xxSimulator.Cli;

internal sealed class CliScenarioPreset
{
    public string? Name { get; set; }
    public string? TestProgramFolderPath { get; set; }
    public string? WiringFolderPath { get; set; }
    public string? SimulationModelFolderPath { get; set; }
    public string? PythonScriptPath { get; set; }
}
