using System;
using System.Collections.Generic;
using System.Linq;

namespace Ct3xxSimulator.Simulation.Waveforms;

public sealed class AppliedWaveform
{
    public AppliedWaveform(
        string signalName,
        string waveformName,
        IReadOnlyList<WaveformPoint> points,
        double sampleTimeMs,
        double delayMs,
        bool periodic,
        int cycles,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        SignalName = string.IsNullOrWhiteSpace(signalName) ? throw new ArgumentException("Signal name must be provided.", nameof(signalName)) : signalName.Trim();
        WaveformName = string.IsNullOrWhiteSpace(waveformName) ? SignalName : waveformName.Trim();
        Points = points ?? Array.Empty<WaveformPoint>();
        SampleTimeMs = sampleTimeMs < 0 ? 0 : sampleTimeMs;
        DelayMs = delayMs < 0 ? 0 : delayMs;
        Periodic = periodic;
        Cycles = cycles <= 0 ? 1 : cycles;
        Metadata = metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string SignalName { get; }
    public string WaveformName { get; }
    public IReadOnlyList<WaveformPoint> Points { get; }
    public double SampleTimeMs { get; }
    public double DelayMs { get; }
    public bool Periodic { get; }
    public int Cycles { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    public double DurationMs
    {
        get
        {
            if (Points.Count <= 1)
            {
                return DelayMs;
            }

            var cycleDuration = Points[^1].TimeMs - Points[0].TimeMs;
            if (cycleDuration <= 0 && SampleTimeMs > 0)
            {
                cycleDuration = SampleTimeMs * Math.Max(Points.Count - 1, 1);
            }

            if (cycleDuration < 0)
            {
                cycleDuration = 0;
            }

            return DelayMs + (cycleDuration * Math.Max(Cycles, 1));
        }
    }

    public double GetValueAt(double relativeTimeMs)
    {
        if (Points.Count == 0)
        {
            return 0;
        }

        if (relativeTimeMs <= DelayMs)
        {
            return Points[0].Value;
        }

        var effectiveTime = relativeTimeMs - DelayMs;
        var cycleDuration = Points.Count > 1
            ? Math.Max(Points[^1].TimeMs - Points[0].TimeMs, SampleTimeMs * Math.Max(Points.Count - 1, 1))
            : 0;

        if (Periodic && cycleDuration > 0)
        {
            effectiveTime %= cycleDuration;
        }
        else if (cycleDuration > 0)
        {
            effectiveTime = Math.Min(effectiveTime, cycleDuration);
        }

        if (effectiveTime <= Points[0].TimeMs)
        {
            return Points[0].Value;
        }

        for (var index = 1; index < Points.Count; index++)
        {
            var previous = Points[index - 1];
            var current = Points[index];
            if (effectiveTime > current.TimeMs)
            {
                continue;
            }

            var span = current.TimeMs - previous.TimeMs;
            if (span <= 0)
            {
                return current.Value;
            }

            var ratio = (effectiveTime - previous.TimeMs) / span;
            return previous.Value + ((current.Value - previous.Value) * ratio);
        }

        return Points[^1].Value;
    }

    public IReadOnlyDictionary<string, object?> Describe()
    {
        var values = Points.Select(item => item.Value).ToList();
        var peak = values.Count == 0 ? 0 : values.Max(item => Math.Abs(item));
        var average = values.Count == 0 ? 0 : values.Average();
        var rms = values.Count == 0 ? 0 : Math.Sqrt(values.Average(item => item * item));
        var min = values.Count == 0 ? 0 : values.Min();
        var max = values.Count == 0 ? 0 : values.Max();

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["signal"] = SignalName,
            ["name"] = WaveformName,
            ["sample_count"] = Points.Count,
            ["sample_time_ms"] = SampleTimeMs,
            ["delay_ms"] = DelayMs,
            ["duration_ms"] = DurationMs,
            ["periodic"] = Periodic,
            ["cycles"] = Cycles,
            ["peak"] = peak,
            ["average"] = average,
            ["rms"] = rms,
            ["min"] = min,
            ["max"] = max,
            ["shape"] = ClassifyShape(values),
            ["metadata"] = Metadata.ToDictionary(item => item.Key, item => (object?)item.Value, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string ClassifyShape(IReadOnlyList<double> values)
    {
        if (values.Count < 3)
        {
            return "dc";
        }

        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1e-9)
        {
            return "dc";
        }

        var distinct = values.Distinct().Count();
        if (distinct <= 3)
        {
            return "square";
        }

        var signChanges = 0;
        for (var index = 1; index < values.Count; index++)
        {
            var deltaPrevious = values[index - 1] - values[Math.Max(0, index - 2)];
            var deltaCurrent = values[index] - values[index - 1];
            if ((deltaPrevious < 0 && deltaCurrent > 0) || (deltaPrevious > 0 && deltaCurrent < 0))
            {
                signChanges++;
            }
        }

        return signChanges >= 4 ? "sine_like" : "custom";
    }
}
