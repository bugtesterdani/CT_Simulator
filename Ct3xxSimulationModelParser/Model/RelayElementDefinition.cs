using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public sealed class RelayElementDefinition : SimulationElementDefinition
{
    public RelayElementDefinition(
        string id,
        RelayCoilDefinition coil,
        IReadOnlyList<RelayContactDefinition> contacts,
        IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, "relay", metadata)
    {
        Coil = coil ?? throw new ArgumentNullException(nameof(coil));
        Contacts = contacts ?? Array.Empty<RelayContactDefinition>();
    }

    public RelayCoilDefinition Coil { get; }
    public IReadOnlyList<RelayContactDefinition> Contacts { get; }
}

public sealed class RelayCoilDefinition
{
    public RelayCoilDefinition(string signal, double thresholdV)
    {
        if (string.IsNullOrWhiteSpace(signal))
        {
            throw new ArgumentException("Relay control signal must be provided.", nameof(signal));
        }

        Signal = signal.Trim();
        ThresholdV = thresholdV;
    }

    public string Signal { get; }
    public double ThresholdV { get; }
}

public sealed class RelayContactDefinition
{
    public RelayContactDefinition(string a, string b, string mode)
    {
        if (string.IsNullOrWhiteSpace(a))
        {
            throw new ArgumentException("Contact pin a must be provided.", nameof(a));
        }

        if (string.IsNullOrWhiteSpace(b))
        {
            throw new ArgumentException("Contact pin b must be provided.", nameof(b));
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            throw new ArgumentException("Contact mode must be provided.", nameof(mode));
        }

        A = a.Trim();
        B = b.Trim();
        Mode = mode.Trim();
    }

    public string A { get; }
    public string B { get; }
    public string Mode { get; }
}
