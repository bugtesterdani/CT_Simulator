using Ct3xxProgramParser.Model;
using Ct3xxTestRunLogParser.Model;

namespace Ct3xxTestRunLogParser.Matching;

/// <summary>
/// Matches visible CT3xx program steps against relevant imported CSV rows.
/// </summary>
public sealed class ImportedTestRunMatcher
{
    private readonly ProgramVisibleStepExtractor _stepExtractor = new();

    /// <summary>
    /// Matches one parsed CT3xx program against one imported CSV test run.
    /// </summary>
    /// <param name="program">The parsed CT3xx program.</param>
    /// <param name="run">The imported CSV test run.</param>
    /// <returns>The match report including coverage and reliability diagnostics.</returns>
    public ImportedTestRunMatchReport Match(Ct3xxProgram program, ImportedTestRun run)
    {
        if (program == null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        if (run == null)
        {
            throw new ArgumentNullException(nameof(run));
        }

        var programSteps = _stepExtractor.Extract(program);
        var csvSteps = run.Steps.Where(IsRelevantCsvStep).ToList();
        var matches = new List<ImportedTestRunStepMatch>();
        var matchedCsvIndexes = new HashSet<int>();

        var searchStartIndex = 0;
        foreach (var programStep in programSteps)
        {
            var bestIndex = -1;
            var bestScore = 0d;
            string? bestReason = null;

            var searchEndIndex = Math.Min(csvSteps.Count - 1, searchStartIndex + 8);
            for (var csvIndex = searchStartIndex; csvIndex <= searchEndIndex; csvIndex++)
            {
                var candidate = csvSteps[csvIndex];
                var (score, reason) = Score(programStep, candidate, csvIndex - searchStartIndex);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = csvIndex;
                    bestReason = reason;
                }
            }

            if (bestIndex < 0 || bestScore < 0.55d)
            {
                continue;
            }

            matches.Add(new ImportedTestRunStepMatch(programStep, csvSteps[bestIndex], bestIndex, bestScore, bestReason ?? "matched"));
            matchedCsvIndexes.Add(bestIndex);
            searchStartIndex = bestIndex + 1;
        }

        var unmatchedProgramSteps = programSteps
            .Where(programStep => matches.All(match => !ReferenceEquals(match.ProgramStep, programStep)))
            .ToList();
        var unmatchedCsvSteps = csvSteps
            .Where((step, index) => !matchedCsvIndexes.Contains(index))
            .ToList();

        return new ImportedTestRunMatchReport(programSteps, csvSteps, matches, unmatchedProgramSteps, unmatchedCsvSteps);
    }

    private static bool IsRelevantCsvStep(ImportedTestRunStep step)
    {
        return step.Kind != ImportedTestRunStepKind.Information &&
               step.Kind != ImportedTestRunStepKind.Unknown;
    }

    private static (double score, string reason) Score(ProgramVisibleStep programStep, ImportedTestRunStep csvStep, int relativeOffset)
    {
        var programName = Normalize(programStep.DisplayName);
        var csvDescription = Normalize(csvStep.Description);
        var csvMessage = Normalize(csvStep.Message ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(programName) && programName == csvDescription)
        {
            return (Math.Max(0.95d, 1d - Math.Min(relativeOffset, 3) * 0.02d), "exact description");
        }

        var descriptionScore = ScoreNormalizedText(programName, csvDescription);
        var messageScore = ScoreNormalizedText(programName, csvMessage);
        var score = Math.Max(descriptionScore, messageScore * 0.9d);

        if (programStep.IsSyntheticEvaluation && csvStep.Kind == ImportedTestRunStepKind.Measurement)
        {
            score += 0.08d;
        }

        if (csvStep.Kind == ImportedTestRunStepKind.Wait && ContainsAny(programName, "warte", "wait"))
        {
            score += 0.1d;
        }

        if (programStep.TestId != null && ContainsAny(programStep.TestId, "PET$") && csvStep.Kind == ImportedTestRunStepKind.Measurement)
        {
            score += 0.05d;
        }

        score -= Math.Min(relativeOffset, 4) * 0.03d;
        score = Math.Max(0d, Math.Min(1d, score));

        return (score, descriptionScore >= messageScore ? "description similarity" : "message similarity");
    }

    private static double ScoreNormalizedText(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0d;
        }

        if (left == right)
        {
            return 1d;
        }

        if (left.IndexOf(right, StringComparison.Ordinal) >= 0 || right.IndexOf(left, StringComparison.Ordinal) >= 0)
        {
            return 0.88d;
        }

        var leftTokens = left.Split(' ').Where(token => token.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var rightTokens = right.Split(' ').Where(token => token.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0d;
        }

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        if (intersection == 0)
        {
            return 0d;
        }

        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();
        return (double)intersection / union;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();
        return string.Join(" ", new string(chars).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(value => source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
