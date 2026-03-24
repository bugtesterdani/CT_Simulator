using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ct3xxSimulator.Simulation.FaultInjection;

public sealed class SimulationFaultSet
{
    public static readonly SimulationFaultSet Empty = new(Array.Empty<SimulationFaultDefinition>(), string.Empty);

    public SimulationFaultSet(IReadOnlyList<SimulationFaultDefinition> faults, string sourcePath)
    {
        Faults = faults;
        SourcePath = sourcePath;
    }

    public IReadOnlyList<SimulationFaultDefinition> Faults { get; }
    public string SourcePath { get; }
    public bool HasFaults => Faults.Count > 0;

    public IReadOnlyList<string> DescribeActiveFaults() =>
        Faults.Where(fault => fault.Enabled).Select(fault => fault.DisplayName).ToList();

    public double? TryGetForcedSignal(string signalName)
    {
        return Faults
            .Where(fault => fault.Enabled &&
                            string.Equals(fault.Type, "force_signal", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(fault.Signal, signalName, StringComparison.OrdinalIgnoreCase))
            .Select(fault => fault.Value)
            .FirstOrDefault(value => value.HasValue);
    }

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

    public bool IsOpenConnection(string a, string b)
    {
        return Faults.Any(fault =>
            fault.Enabled &&
            string.Equals(fault.Type, "open_connection", StringComparison.OrdinalIgnoreCase) &&
            MatchesConnection(fault, a, b));
    }

    public bool IsShortConnection(string a, string b)
    {
        return Faults.Any(fault =>
            fault.Enabled &&
            string.Equals(fault.Type, "short_connection", StringComparison.OrdinalIgnoreCase) &&
            MatchesConnection(fault, a, b));
    }

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

    public bool IsBlownFuse(string elementId)
    {
        return Faults.Any(fault =>
            fault.Enabled &&
            string.Equals(fault.Type, "blow_fuse", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(fault.ElementId, elementId, StringComparison.OrdinalIgnoreCase));
    }

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

    public double? TryGetResistanceOverride(string elementId)
    {
        return Faults
            .Where(fault => fault.Enabled &&
                            string.Equals(fault.Type, "wrong_resistance", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(fault.ElementId, elementId, StringComparison.OrdinalIgnoreCase))
            .Select(fault => fault.Value)
            .FirstOrDefault(value => value.HasValue);
    }

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
        public List<SimulationFaultDefinition>? Faults { get; set; }
    }
}
