// Provides Assembly Element Definition for the simulation model parser model support.
using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the assembly element definition.
/// </summary>
public sealed class AssemblyElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyElementDefinition"/> class.
    /// </summary>
    public AssemblyElementDefinition(
        string id,
        string wiring,
        string? simulation,
        IReadOnlyDictionary<string, string> ports,
        IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "assembly", metadata)
    {
        if (string.IsNullOrWhiteSpace(wiring))
        {
            throw new ArgumentException("Assembly wiring path must be provided.", nameof(wiring));
        }

        Wiring = wiring.Trim();
        Simulation = string.IsNullOrWhiteSpace(simulation) ? null : simulation!.Trim();
        Ports = ports;
    }

    /// <summary>
    /// Gets the wiring.
    /// </summary>
    public string Wiring { get; }
    /// <summary>
    /// Gets the simulation.
    /// </summary>
    public string? Simulation { get; }
    /// <summary>
    /// Gets the ports.
    /// </summary>
    public IReadOnlyDictionary<string, string> Ports { get; }
}
