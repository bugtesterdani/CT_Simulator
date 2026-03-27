using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation.FaultInjection;

/// <summary>
/// Describes one declarative fault entry loaded from <c>faults.json</c>.
/// </summary>
public sealed class SimulationFaultDefinition
{
    /// <summary>
    /// Gets the unique identifier of the fault entry.
    /// </summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>
    /// Gets the fault type, for example <c>force_signal</c> or <c>open_connection</c>.
    /// </summary>
    public string Type { get; init; } = string.Empty;
    /// <summary>
    /// Gets a value indicating whether the fault is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;
    /// <summary>
    /// Gets the logical signal name targeted by the fault, if applicable.
    /// </summary>
    public string? Signal { get; init; }
    /// <summary>
    /// Gets the numeric fault value, if applicable.
    /// </summary>
    public double? Value { get; init; }
    /// <summary>
    /// Gets the targeted simulation element identifier, if applicable.
    /// </summary>
    public string? ElementId { get; init; }
    /// <summary>
    /// Gets the textual state requested by the fault, if applicable.
    /// </summary>
    public string? State { get; init; }
    /// <summary>
    /// Gets the first endpoint of a connection-oriented fault.
    /// </summary>
    public string? A { get; init; }
    /// <summary>
    /// Gets the second endpoint of a connection-oriented fault.
    /// </summary>
    public string? B { get; init; }
    /// <summary>
    /// Gets additional fault metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a concise display name for diagnostics.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Id) ? Type : $"{Id} ({Type})";
}
