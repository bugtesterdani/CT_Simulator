namespace Ct3xxSimulator.Desktop.Configuration;

/// <summary>
/// Defines how an optional imported CSV test run should be used inside the desktop app.
/// </summary>
public enum CsvReplayMode
{
    /// <summary>
    /// Disables CSV replay support.
    /// </summary>
    Off,

    /// <summary>
    /// Loads CSV data for comparison without overriding simulator results.
    /// </summary>
    Compare,

    /// <summary>
    /// Prefers imported CSV results over simulator results once the replay integration is active.
    /// </summary>
    CsvDrivesResult
}
