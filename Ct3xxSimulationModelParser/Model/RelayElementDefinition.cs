// Provides Relay Element Definition for the simulation model parser model support.
using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the relay element definition.
/// </summary>
public sealed class RelayElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelayElementDefinition"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the coil.
    /// </summary>
    public RelayCoilDefinition Coil { get; }
    /// <summary>
    /// Gets the contacts.
    /// </summary>
    public IReadOnlyList<RelayContactDefinition> Contacts { get; }
}

/// <summary>
/// Represents the relay coil definition.
/// </summary>
public sealed class RelayCoilDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCoilDefinition"/> class.
    /// </summary>
    public RelayCoilDefinition(string signal, double thresholdV)
    {
        if (string.IsNullOrWhiteSpace(signal))
        {
            throw new ArgumentException("Relay control signal must be provided.", nameof(signal));
        }

        Signal = signal.Trim();
        ThresholdV = thresholdV;
    }

    /// <summary>
    /// Gets the signal.
    /// </summary>
    public string Signal { get; }
    /// <summary>
    /// Gets the threshold v.
    /// </summary>
    public double ThresholdV { get; }
}

/// <summary>
/// Represents the relay contact definition.
/// </summary>
public sealed class RelayContactDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelayContactDefinition"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the a.
    /// </summary>
    public string A { get; }
    /// <summary>
    /// Gets the b.
    /// </summary>
    public string B { get; }
    /// <summary>
    /// Gets the mode.
    /// </summary>
    public string Mode { get; }
}
