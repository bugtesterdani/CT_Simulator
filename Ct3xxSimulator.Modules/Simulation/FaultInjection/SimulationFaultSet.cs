using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ct3xxSimulator.Simulation.FaultInjection;

/// <summary>
/// Provides lookup and evaluation helpers for the faults active in one simulation run.
/// </summary>
public sealed class SimulationFaultSet
{
    /// <summary>
    /// Gets an empty fault set.
    /// </summary>
    public static readonly SimulationFaultSet Empty = new(Array.Empty<SimulationFaultDefinition>(), string.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationFaultSet"/> class.
    /// </summary>
    /// <param name="faults">The loaded fault definitions.</param>
    /// <param name="sourcePath">The source file path from which the faults were loaded.</param>
    public SimulationFaultSet(IReadOnlyList<SimulationFaultDefinition> faults, string sourcePath)
    {
        Faults = faults;
        SourcePath = sourcePath;
    }

    /// <summary>
    /// Gets the loaded fault definitions.
    /// </summary>
    public IReadOnlyList<SimulationFaultDefinition> Faults { get; }
    /// <summary>
    /// Gets the source file path from which the faults were loaded.
    /// </summary>
    public string SourcePath { get; }
    /// <summary>
    /// Gets a value indicating whether any enabled faults are active.
    /// </summary>
    public bool HasFaults => Faults.Count > 0;

    /// <summary>
    /// Produces display text for all enabled faults.
    /// </summary>
    public IReadOnlyList<string> DescribeActiveFaults() =>
        Faults.Where(fault => fault.Enabled).Select(fault => fault.DisplayName).ToList();

    /// <summary>
    /// Resolves a forced value for one logical signal, if configured.
    /// </summary>
    public double? TryGetForcedSignal(string signalName)
    {
        return Faults
            .Where(fault => fault.Enabled &&
                            string.Equals(fault.Type, "force_signal", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(fault.Signal, signalName, StringComparison.OrdinalIgnoreCase))
            .Select(fault => fault.Value)
            .FirstOrDefault(value => value.HasValue);
    }

    /// <summary>
    /// Resolves an enforced relay state for one relay element, if configured.
    /// </summary>
    public bool? TryGetForcedRelayState(string elementId)
    {
        var state = Faults
            .FirstOrDefault(fault => fault.Enabled &&
                                     string.Equals(fault.Type, "force_relay", StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(fault.ElementId, elementId, StringComparison.OrdinalIgnoreCase))
            ?.State;
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        return state.Equals("closed", StringComparison.OrdinalIgnoreCase)
            ? true
            : state.Equals("open", StringComparison.OrdinalIgnoreCase)
                ? false
                : null;
    }

    /// <summary>
    /// Determines whether the connection between two endpoints is forced open.
    /// </summary>
    public bool IsOpenConnection(string a, string b)
    {
        return Faults.Any(fault =>
            fault.Enabled &&
            string.Equals(fault.Type, "open_connection", StringComparison.OrdinalIgnoreCase) &&
            MatchesConnection(fault, a, b));
    }

    /// <summary>
    /// Determines whether the connection between two endpoints is forced shorted.
    /// </summary>
    public bool IsShortConnection(string a, string b)
    {
        return Faults.Any(fault =>
            fault.Enabled &&
            string.Equals(fault.Type, "short_connection", StringComparison.OrdinalIgnoreCase) &&
            MatchesConnection(fault, a, b));
    }

    /// <summary>
    /// Determines whether the connection between two endpoints currently exhibits a contact problem.
    /// </summary>
    public bool HasContactProblem(string a, string b, long currentTimeMs)
    {
        var fault = Faults.FirstOrDefault(item =>
            item.Enabled &&
            string.Equals(item.Type, "contact_problem", StringComparison.OrdinalIgnoreCase) &&
            MatchesConnection(item, a, b));
        if (fault == null)
        {
            return false;
        }

        var mode = fault.Metadata.TryGetValue("mode", out var modeText)
            ? (modeText ?? string.Empty).Trim().ToLowerInvariant()
            : "toggle";
        if (mode == "stuck_open")
        {
            return true;
        }

        if (mode == "stuck_closed")
        {
            return false;
        }

        if (mode == "random")
        {
            var periodMs = Math.Max(1L, TryReadLong(fault, "period_ms", 250));
            var hash = HashCode.Combine(a.ToUpperInvariant(), b.ToUpperInvariant(), currentTimeMs / periodMs, fault.Id.ToUpperInvariant());
            var thresholdPercent = fault.Value ?? 50d;
            var sample = Math.Abs(hash % 100);
            return sample < thresholdPercent;
        }

        var closedMs = TryReadLong(fault, "closed_ms", 250);
        var openMs = TryReadLong(fault, "open_ms", 250);
        var cycle = Math.Max(1L, closedMs + openMs);
        var phase = Math.Abs(currentTimeMs) % cycle;
        return phase >= closedMs;
    }

    /// <summary>
    /// Determines whether a fuse element is considered blown.
    /// </summary>
    public bool IsBlownFuse(string elementId)
    {
        return Faults.Any(fault =>
            fault.Enabled &&
            string.Equals(fault.Type, "blow_fuse", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(fault.ElementId, elementId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Applies configured signal drift faults to a numeric signal value.
    /// </summary>
    public double ApplySignalDrift(string signalName, double value)
    {
        var driftFaults = Faults
            .Where(fault => fault.Enabled &&
                            string.Equals(fault.Type, "signal_drift", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(fault.Signal, signalName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (driftFaults.Count == 0)
        {
            return value;
        }

        var current = value;
        foreach (var fault in driftFaults)
        {
            var amount = fault.Value ?? 0d;
            var mode = fault.Metadata.TryGetValue("mode", out var modeText)
                ? (modeText ?? string.Empty).Trim().ToLowerInvariant()
                : "add";
            current = mode switch
            {
                "set" => amount,
                "scale" => current * amount,
                _ => current + amount
            };
        }

        return current;
    }

    /// <summary>
    /// Resolves an explicit resistance override for one element, if configured.
    /// </summary>
    public double? TryGetResistanceOverride(string elementId)
    {
        return Faults
            .Where(fault => fault.Enabled &&
                            string.Equals(fault.Type, "wrong_resistance", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(fault.ElementId, elementId, StringComparison.OrdinalIgnoreCase))
            .Select(fault => fault.Value)
            .FirstOrDefault(value => value.HasValue);
    }

    /// <summary>
    /// Resolves the effective resistance of one element including any active override fault.
    /// </summary>
    public double ResolveResistanceValue(string elementId, double baseValue)
    {
        var fault = Faults.FirstOrDefault(item =>
            item.Enabled &&
            string.Equals(item.Type, "wrong_resistance", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.ElementId, elementId, StringComparison.OrdinalIgnoreCase));
        if (fault?.Value == null)
        {
            return baseValue;
        }

        var mode = fault.Metadata.TryGetValue("mode", out var modeText)
            ? modeText ?? string.Empty
            : "set";

        return mode.ToLowerInvariant() switch
        {
            "scale" => baseValue * fault.Value.Value,
            "add" => baseValue + fault.Value.Value,
            _ => fault.Value.Value
        };
    }

    /// <summary>
    /// Loads faults from a <c>faults.json</c> file located in the specified directory.
    /// </summary>
    public static SimulationFaultSet Load(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return Empty;
        }

        var path = Path.Combine(rootDirectory, "faults.json");
        if (!File.Exists(path))
        {
            return Empty;
        }

        var document = JsonSerializer.Deserialize<FaultDocument>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return new SimulationFaultSet(document?.Faults?.Where(f => f.Enabled).ToList() ?? new List<SimulationFaultDefinition>(), path);
    }

    private static long TryReadLong(SimulationFaultDefinition fault, string key, long defaultValue)
    {
        return fault.Metadata.TryGetValue(key, out var text) &&
               long.TryParse(text, out var parsed)
            ? Math.Max(0L, parsed)
            : defaultValue;
    }

    private static bool MatchesConnection(SimulationFaultDefinition fault, string a, string b)
    {
        return (string.Equals(fault.A, a, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(fault.B, b, StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(fault.A, b, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(fault.B, a, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FaultDocument
    {
        /// <summary>
        /// Gets the faults.
        /// </summary>
        public List<SimulationFaultDefinition>? Faults { get; set; }
    }
}
