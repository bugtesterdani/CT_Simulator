// Provides Transformer Element Definition for the simulation model parser model support.
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the transformer element definition.
/// </summary>
public sealed class TransformerElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransformerElementDefinition"/> class.
    /// </summary>
    public TransformerElementDefinition(
        string id,
        string primaryA,
        string primaryB,
        string secondaryA,
        string secondaryB,
        double ratio,
        IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "transformer", metadata)
    {
        PrimaryA = primaryA;
        PrimaryB = primaryB;
        SecondaryA = secondaryA;
        SecondaryB = secondaryB;
        Ratio = ratio;
    }

    /// <summary>
    /// Gets the primary a.
    /// </summary>
    public string PrimaryA { get; }
    /// <summary>
    /// Gets the primary b.
    /// </summary>
    public string PrimaryB { get; }
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
