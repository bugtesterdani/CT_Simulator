using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Captures the externally simulated device state at one simulated point in time.
/// </summary>
public sealed class ExternalDeviceStateSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalDeviceStateSnapshot"/> class.
    /// </summary>
    /// <param name="timeMs">The simulated device time in milliseconds.</param>
    /// <param name="inputs">The currently applied device inputs.</param>
    /// <param name="sources">The active source values exposed by the device model.</param>
    /// <param name="internalSignals">The internal device state values.</param>
    /// <param name="outputs">The current device outputs.</param>
    /// <param name="interfaces">The current interface states or last interface payloads.</param>
    public ExternalDeviceStateSnapshot(
        long timeMs,
        IReadOnlyDictionary<string, string>? inputs = null,
        IReadOnlyDictionary<string, string>? sources = null,
        IReadOnlyDictionary<string, string>? internalSignals = null,
        IReadOnlyDictionary<string, string>? outputs = null,
        IReadOnlyDictionary<string, string>? interfaces = null)
    {
        TimeMs = timeMs;
        Inputs = inputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Sources = sources ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        InternalSignals = internalSignals ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Outputs = outputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Interfaces = interfaces ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the simulated device time in milliseconds.
    /// </summary>
    public long TimeMs { get; }

    /// <summary>
    /// Gets the current external inputs written to the device model.
    /// </summary>
    public IReadOnlyDictionary<string, string> Inputs { get; }

    /// <summary>
    /// Gets the current source values exposed by the device model.
    /// </summary>
    public IReadOnlyDictionary<string, string> Sources { get; }

    /// <summary>
    /// Gets the internal device state values published by the device model.
    /// </summary>
    public IReadOnlyDictionary<string, string> InternalSignals { get; }

    /// <summary>
    /// Gets the current device outputs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Outputs { get; }

    /// <summary>
    /// Gets the current interface states or payloads.
    /// </summary>
    public IReadOnlyDictionary<string, string> Interfaces { get; }

    /// <summary>
    /// Gets an empty snapshot instance.
    /// </summary>
    public static ExternalDeviceStateSnapshot Empty { get; } = new(0);
}
