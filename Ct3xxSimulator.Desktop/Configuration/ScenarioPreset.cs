namespace Ct3xxSimulator.Desktop.Configuration;

public sealed class ScenarioPreset
{
    public string Name { get; set; } = string.Empty;
    public string? TestProgramFolderPath { get; set; }
    public string? WiringFolderPath { get; set; }
    public string? SimulationModelFolderPath { get; set; }
    public string? PythonScriptPath { get; set; }
}
