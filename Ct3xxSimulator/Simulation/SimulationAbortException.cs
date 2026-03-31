// Provides Simulation Abort Exception for explicit stop conditions.
using System;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Represents a simulation stop that should abort the active run.
/// </summary>
public sealed class SimulationAbortException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationAbortException"/> class.
    /// </summary>
    /// <param name="reason">The abort reason.</param>
    /// <param name="message">The formatted message.</param>
    public SimulationAbortException(SimulationAbortReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the abort reason.
    /// </summary>
    public SimulationAbortReason Reason { get; }
}

/// <summary>
/// Defines the known reasons for aborting a simulation.
/// </summary>
public enum SimulationAbortReason
{
    /// <summary>
    /// Indicates a configured output limit was exceeded.
    /// </summary>
    OutputLimitViolation
}
