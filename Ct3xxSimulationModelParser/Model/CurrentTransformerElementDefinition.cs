using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public sealed class CurrentTransformerElementDefinition : SimulationElementDefinition
{
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

    public string PrimarySignal { get; }
    public string SecondaryA { get; }
    public string SecondaryB { get; }
    public double Ratio { get; }
}
