using Ct3xxProgramParser.Model;
using Ct3xxTestRunLogParser.Model;

namespace Ct3xxTestRunLogParser.Matching;

/// <summary>
/// Extracts the visible simulator step sequence from a parsed CT3xx program.
/// </summary>
public sealed class ProgramVisibleStepExtractor
{
    /// <summary>
    /// Builds the visible step sequence for one parsed CT3xx program.
    /// </summary>
    /// <param name="program">The parsed CT3xx program.</param>
    /// <returns>The visible simulator steps in execution order.</returns>
    public IReadOnlyList<ProgramVisibleStep> Extract(Ct3xxProgram program)
    {
        if (program == null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        var result = new List<ProgramVisibleStep>();
        var sequenceIndex = 0;

        foreach (var item in program.RootItems)
        {
            ExtractSequenceNode(item, "root", null, result, ref sequenceIndex);
        }

        if (program.DutLoop != null)
        {
            var loopName = program.DutLoop.Name ?? "DUT Loop";
            foreach (var item in program.DutLoop.Items)
            {
                ExtractSequenceNode(item, "dutloop", loopName, result, ref sequenceIndex);
            }
        }

        return result;
    }

    private static void ExtractSequenceNode(
        SequenceNode node,
        string parentNodePath,
        string? parentGroupPath,
        ICollection<ProgramVisibleStep> result,
        ref int sequenceIndex)
    {
        switch (node)
        {
            case Group group:
                var groupName = group.Name ?? group.Id ?? "Gruppe";
                var groupPath = string.IsNullOrWhiteSpace(parentGroupPath)
                    ? groupName
                    : parentGroupPath + " > " + groupName;
                var groupNodePath = parentNodePath + "/group:" + groupName;
                foreach (var child in group.Items)
                {
                    ExtractSequenceNode(child, groupNodePath, groupPath, result, ref sequenceIndex);
                }

                break;

            case Test test:
                AppendVisibleSteps(test, parentNodePath, parentGroupPath, result, ref sequenceIndex);

                break;
        }
    }

    private static void AppendVisibleSteps(
        Test test,
        string parentNodePath,
        string? groupPath,
        ICollection<ProgramVisibleStep> result,
        ref int sequenceIndex)
    {
        var baseName = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        var baseNodePath = parentNodePath + "/test:" + baseName;

        if (string.Equals(test.Id, "PET$", StringComparison.OrdinalIgnoreCase))
        {
            var labels = test.Parameters?.Tables
                .SelectMany(table => table.Records)
                .Where(record => !string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
                .Select(record => record.DrawingReference ?? record.Expression ?? record.Id ?? baseName)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();

            if (labels != null && labels.Count > 0)
            {
                for (var index = 0; index < labels.Count; index++)
                {
                    result.Add(new ProgramVisibleStep(
                        sequenceIndex,
                        labels[index]!,
                        baseNodePath + "/eval:" + index,
                        test.Id,
                        baseName,
                        groupPath,
                        test,
                        isSyntheticEvaluation: true));
                    sequenceIndex++;
                }

                return;
            }
        }

        result.Add(new ProgramVisibleStep(
            sequenceIndex,
            baseName,
            baseNodePath,
            test.Id,
            baseName,
            groupPath,
            test,
            isSyntheticEvaluation: false));
        sequenceIndex++;

        foreach (var child in test.Items)
        {
            ExtractSequenceNode(child, baseNodePath, groupPath, result, ref sequenceIndex);
        }
    }
}
