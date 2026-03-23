using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public sealed class UnknownElementDefinition : SimulationElementDefinition
{
    public UnknownElementDefinition(string id, string type, IReadOnlyDictionary<string, string?> metadata)
        : base(id, type, metadata)
    {
    }
}
