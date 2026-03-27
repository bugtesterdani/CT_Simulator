// Provides Resistor Element Definition for the simulation model parser model support.
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the resistor element definition.
/// </summary>
public sealed class ResistorElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResistorElementDefinition"/> class.
    /// </summary>
    public ResistorElementDefinition(string id, string a, string b, double ohms, IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "resistor", metadata)
    {
        A = a;
        B = b;
        Ohms = ohms;
    }

    /// <summary>
    /// Gets the a.
    /// </summary>
    public string A { get; }
    /// <summary>
    /// Gets the b.
    /// </summary>
    public string B { get; }
    /// <summary>
    /// Gets the ohms.
    /// </summary>
    public double Ohms { get; }
}
