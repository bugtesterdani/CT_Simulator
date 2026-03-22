using System;
using System.Collections.Generic;
using System.Linq;

namespace Ct3xxProgramParser.SignalTables;

public sealed class SignalTable
{
    public SignalTable(string? sourcePath, IReadOnlyList<SignalModule> modules)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath;
        Modules = modules ?? Array.Empty<SignalModule>();
    }

    public string? SourcePath { get; }
    public IReadOnlyList<SignalModule> Modules { get; }

    public IEnumerable<SignalAssignment> AllAssignments => Modules.SelectMany(module => module.Assignments);
}
