// Provides Current Transformer Element Definition for the simulation model parser model support.
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the current transformer element definition.
/// </summary>
public sealed class CurrentTransformerElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentTransformerElementDefinition"/> class.
    /// </summary>
    public CurrentTransformerElementDefinition(
        string id,
        string primarySignal,
        string secondaryA,
        string secondaryB,
        double ratio,
        IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "current_transformer", metadata)
    {
        PrimarySignal = primarySignal;
        SecondaryA = secondaryA;
        SecondaryB = secondaryB;
        Ratio = ratio;
    }

    /// <summary>
    /// Gets the primary signal.
    /// </summary>
    public string PrimarySignal { get; }
    /// <summary>
    /// Gets the secondary a.
    /// </summary>
    public string SecondaryA { get; }
    /// <summary>
    /// Gets the secondary b.
    /// </summary>
    public string SecondaryB { get; }
    /// <summary>
    /// Gets the ratio.
    /// </summary>
    public double Ratio { get; }
}
