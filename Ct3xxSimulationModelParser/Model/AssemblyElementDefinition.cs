using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public sealed class AssemblyElementDefinition : SimulationElementDefinition
{
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
        Simulation = string.IsNullOrWhiteSpace(simulation) ? null : simulation.Trim();
        Ports = ports;
    }

    public string Wiring { get; }
    public string? Simulation { get; }
    public IReadOnlyDictionary<string, string> Ports { get; }
}
