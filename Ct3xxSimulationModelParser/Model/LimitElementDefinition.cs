// Provides Limit Element Definition for the simulation model parser model support.
using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents a signal limit element definition.
/// </summary>
public sealed class LimitElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LimitElementDefinition"/> class.
    /// </summary>
    public LimitElementDefinition(
        string id,
        string mode,
        IReadOnlyList<string> nodePrefixes,
        double? maxVoltage,
        double? maxCurrent,
        double gain,
        IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "limit", metadata)
    {
        Mode = string.IsNullOrWhiteSpace(mode) ? "voltage" : mode.Trim();
        NodePrefixes = nodePrefixes ?? Array.Empty<string>();
        MaxVoltage = maxVoltage;
        MaxCurrent = maxCurrent;
        Gain = Math.Abs(gain) < double.Epsilon ? 1d : gain;
    }

    /// <summary>
    /// Gets the limit mode.
    /// </summary>
    public string Mode { get; }
    /// <summary>
    /// Gets the node prefixes the limit applies to.
    /// </summary>
    public IReadOnlyList<string> NodePrefixes { get; }
    /// <summary>
    /// Gets the maximum voltage allowed at the output.
    /// </summary>
    public double? MaxVoltage { get; }
    /// <summary>
    /// Gets the maximum current allowed at the output.
    /// </summary>
    public double? MaxCurrent { get; }
    /// <summary>
    /// Gets the gain applied to the written value before checking.
    /// </summary>
    public double Gain { get; }
}
