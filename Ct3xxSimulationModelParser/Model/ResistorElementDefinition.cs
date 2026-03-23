using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public sealed class ResistorElementDefinition : SimulationElementDefinition
{
    public ResistorElementDefinition(string id, string a, string b, double ohms, IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "resistor", metadata)
    {
        A = a;
        B = b;
        Ohms = ohms;
    }

    public string A { get; }
    public string B { get; }
    public double Ohms { get; }
}
