// Provides Signal Table for the program parser signal table support.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ct3xxProgramParser.SignalTables;

/// <summary>
/// Represents the signal table.
/// </summary>
public sealed class SignalTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalTable"/> class.
    /// </summary>
    public SignalTable(string? sourcePath, IReadOnlyList<SignalModule> modules)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath;
        Modules = modules ?? Array.Empty<SignalModule>();
    }

    /// <summary>
    /// Gets the source path.
    /// </summary>
    public string? SourcePath { get; }
    /// <summary>
    /// Gets the modules.
    /// </summary>
    public IReadOnlyList<SignalModule> Modules { get; }

    /// <summary>
    /// Gets the all assignments.
    /// </summary>
    public IEnumerable<SignalAssignment> AllAssignments => Modules.SelectMany(module => module.Assignments);
}
