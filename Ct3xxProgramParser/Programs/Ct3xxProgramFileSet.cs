// Provides Ct3xx Program File Set for the program parser program loading support.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Programs;

/// <summary>
/// Represents the ct3xx program file set.
/// </summary>
public sealed class Ct3xxProgramFileSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Ct3xxProgramFileSet"/> class.
    /// </summary>
    public Ct3xxProgramFileSet(string programPath, Ct3xxProgram program, IReadOnlyList<Ct3xxFileDocument> externalFiles)
    {
        if (string.IsNullOrWhiteSpace(programPath))
        {
            throw new ArgumentException("Program path must be provided.", nameof(programPath));
        }

        ProgramPath = Path.GetFullPath(programPath);
        ProgramDirectory = Path.GetDirectoryName(ProgramPath) ?? Directory.GetCurrentDirectory();
        Program = program ?? throw new ArgumentNullException(nameof(program));
        ExternalFiles = externalFiles ?? Array.Empty<Ct3xxFileDocument>();
    }

    /// <summary>
    /// Gets the program path.
    /// </summary>
    public string ProgramPath { get; }
    /// <summary>
    /// Gets the program directory.
    /// </summary>
    public string ProgramDirectory { get; }
    /// <summary>
    /// Gets the program.
    /// </summary>
    public Ct3xxProgram Program { get; }
    /// <summary>
    /// Gets the external files.
    /// </summary>
    public IReadOnlyList<Ct3xxFileDocument> ExternalFiles { get; }

    /// <summary>
    /// Gets the ct3xx file document.
    /// </summary>
    public IEnumerable<TDocument> GetDocuments<TDocument>() where TDocument : Ct3xxFileDocument =>
        ExternalFiles.OfType<TDocument>();

    /// <summary>
    /// Gets the signal tables.
    /// </summary>
    public IEnumerable<SignalTableDocument> SignalTables => GetDocuments<SignalTableDocument>();
}
