using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation.FaultInjection;

public sealed class SimulationFaultDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public string? Signal { get; init; }
    public double? Value { get; init; }
    public string? ElementId { get; init; }
    public string? State { get; init; }
    public string? A { get; init; }
    public string? B { get; init; }
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(Id) ? Type : $"{Id} ({Type})";
}
