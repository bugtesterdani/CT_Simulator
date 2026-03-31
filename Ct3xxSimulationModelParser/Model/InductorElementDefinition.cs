// Provides Inductor Element Definition for the simulation model parser model support.
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the inductor element definition.
/// </summary>
public sealed class InductorElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InductorElementDefinition"/> class.
    /// </summary>
    public InductorElementDefinition(string id, string a, string b, double henry, IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "inductor", metadata)
    {
        A = a;
        B = b;
        Henry = henry;
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
    /// Gets the henry.
    /// </summary>
    public double Henry { get; }
}
