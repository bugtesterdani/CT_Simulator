using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Programs;

public sealed class Ct3xxProgramFileSet
{
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

    public string ProgramPath { get; }
    public string ProgramDirectory { get; }
    public Ct3xxProgram Program { get; }
    public IReadOnlyList<Ct3xxFileDocument> ExternalFiles { get; }

    public IEnumerable<TDocument> GetDocuments<TDocument>() where TDocument : Ct3xxFileDocument =>
        ExternalFiles.OfType<TDocument>();

    public IEnumerable<SignalTableDocument> SignalTables => GetDocuments<SignalTableDocument>();
}
