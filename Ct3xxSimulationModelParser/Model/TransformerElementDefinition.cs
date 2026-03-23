using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public sealed class TransformerElementDefinition : SimulationElementDefinition
{
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

    public string PrimaryA { get; }
    public string PrimaryB { get; }
    public string SecondaryA { get; }
    public string SecondaryB { get; }
    public double Ratio { get; }
}
