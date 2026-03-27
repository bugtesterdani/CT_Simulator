namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Defines the normalized outcome states used for simulated test execution.
/// </summary>
public enum TestOutcome
{
    /// <summary>
    /// Indicates that the simulated test satisfied its expected limits or conditions.
    /// </summary>
    Pass,

    /// <summary>
    /// Indicates that the simulated test completed but violated its expected limits or conditions.
    /// </summary>
    Fail,

    /// <summary>
    /// Indicates that the simulated test could not be evaluated because of a runtime or configuration error.
    /// </summary>
    Error
}
