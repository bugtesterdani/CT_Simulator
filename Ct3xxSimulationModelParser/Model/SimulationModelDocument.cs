using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public sealed class SimulationModelDocument
{
    public SimulationModelDocument(string? sourcePath, IReadOnlyList<SimulationElementDefinition> elements)
    {
        SourcePath = sourcePath;
        Elements = elements ?? Array.Empty<SimulationElementDefinition>();
    }

    public string? SourcePath { get; }
    public IReadOnlyList<SimulationElementDefinition> Elements { get; }
}
