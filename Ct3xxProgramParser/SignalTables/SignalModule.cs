using System;
using System.Collections.Generic;

namespace Ct3xxProgramParser.SignalTables;

public sealed class SignalModule
{
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

    public string Name { get; }
    public string? Description { get; }
    public IReadOnlyList<SignalAssignment> Assignments { get; }
}
