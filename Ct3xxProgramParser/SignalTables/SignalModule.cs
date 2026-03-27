// Provides Signal Module for the program parser signal table support.
using System;
using System.Collections.Generic;

namespace Ct3xxProgramParser.SignalTables;

/// <summary>
/// Represents the signal module.
/// </summary>
public sealed class SignalModule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalModule"/> class.
    /// </summary>
    public SignalModule(string name, string? description, IReadOnlyList<SignalAssignment> assignments)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Module name must not be empty.", nameof(name));
        }

        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        Assignments = assignments ?? Array.Empty<SignalAssignment>();
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the description.
    /// </summary>
    public string? Description { get; }
    /// <summary>
    /// Gets the assignments.
    /// </summary>
    public IReadOnlyList<SignalAssignment> Assignments { get; }
}
