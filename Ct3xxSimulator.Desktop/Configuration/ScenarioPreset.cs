// Provides Scenario Preset for the desktop application configuration support.
using System.Collections.Generic;

namespace Ct3xxSimulator.Desktop.Configuration;

/// <summary>
/// Represents the scenario preset.
/// </summary>
public sealed class ScenarioPreset
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets the test program folder path.
    /// </summary>
    public string? TestProgramFolderPath { get; set; }
    /// <summary>
    /// Gets the selected program file name.
    /// </summary>
    public string? SelectedProgramFileName { get; set; }
    /// <summary>
    /// Gets the selected program file checksum sha256.
    /// </summary>
    public string? SelectedProgramFileChecksumSha256 { get; set; }
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
    /// Gets the optional imported CSV replay file path.
    /// </summary>
    public string? CsvReplayFilePath { get; set; }
    /// <summary>
    /// Gets the selected CSV replay mode.
    /// </summary>
    public CsvReplayMode CsvReplayMode { get; set; }
    /// <summary>
    /// Executes new.
    /// </summary>
    public List<string> BreakpointKeys { get; set; } = new();
}
