// Provides tester card inventory validation logic.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Validation;

public static class CardInventoryValidator
{
    /// <summary>
    /// Executes HasCardConfiguration.
    /// </summary>
    public static bool HasCardConfiguration(CardInventoryDefinition? definition)
    {
        if (definition == null)
        {
            return false;
        }

        return (definition.TestTypeCards?.Count ?? 0) > 0 ||
               (definition.TestTypeCardRules?.Count ?? 0) > 0 ||
               (definition.InstalledCards?.Count ?? 0) > 0;
    }

    /// <summary>
    /// Executes Validate.
    /// </summary>
    public static IReadOnlyList<string> Validate(Ct3xxProgram program, CardInventoryDefinition definition)
    {
        if (program == null || definition == null)
        {
            return Array.Empty<string>();
        }

        var testTypeCards = definition.TestTypeCards ?? new Dictionary<string, string>();
        var rules = definition.TestTypeCardRules ?? new List<TestTypeCardRule>();
        var installedCards = definition.InstalledCards ?? new Dictionary<string, int>();
        var indexPatterns = definition.CardIndexPatterns ?? new Dictionary<string, string>();

        var requiredCards = BuildRequiredCards(program, testTypeCards, rules, indexPatterns);
        var issues = new List<string>();
        foreach (var required in requiredCards)
        {
            if (string.Equals(required.Key, "PC", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installed = installedCards.TryGetValue(required.Key, out var count) ? count : 0;
            if (installed < required.Value.RequiredCount)
            {
                issues.Add($"Karte '{required.Key}' nicht ausreichend vorhanden: benoetigt {required.Value.RequiredCount}, installiert {installed}.");
            }
        }

        return issues;
    }

    /// <summary>
    /// Executes BuildRequiredCards.
    /// </summary>
    private static Dictionary<string, RequiredCardUsage> BuildRequiredCards(
        Ct3xxProgram program,
        Dictionary<string, string> testTypeCards,
        List<TestTypeCardRule> rules,
        Dictionary<string, string> cardIndexPatterns)
    {
        var required = new Dictionary<string, RequiredCardUsage>(StringComparer.OrdinalIgnoreCase);
        var normalizedTestTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in testTypeCards)
        {
            if (string.IsNullOrWhiteSpace(mapping.Key) || string.IsNullOrWhiteSpace(mapping.Value))
            {
                continue;
            }

            normalizedTestTypes[mapping.Key.Trim()] = mapping.Value.Trim();
        }

        foreach (var test in EnumerateTests(program))
        {
            var testType = test.Id?.Trim();
            if (string.IsNullOrWhiteSpace(testType) || !normalizedTestTypes.TryGetValue(testType, out var cardName))
            {
                cardName = string.Empty;
            }

            var cards = ResolveCardsForTest(testType, test, cardName, rules);
            if (cards.Count == 0)
            {
                continue;
            }

            foreach (var card in cards)
            {
                if (string.IsNullOrWhiteSpace(card))
                {
                    continue;
                }

                if (!required.TryGetValue(card, out var usage))
                {
                    usage = new RequiredCardUsage(card);
                    required[card] = usage;
                }

                usage.MarkUsed(testType ?? string.Empty);

                if (string.Equals(card, "PC", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var indices = ExtractCardIndices(card, test, cardIndexPatterns);
                usage.RegisterIndices(indices);
            }
        }

        return required;
    }

    /// <summary>
    /// Executes ResolveCardsForTest.
    /// </summary>
    private static List<string> ResolveCardsForTest(
        string? testType,
        Test test,
        string fallbackCardName,
        List<TestTypeCardRule> rules)
    {
        var cards = new List<string>();
        if (string.IsNullOrWhiteSpace(testType))
        {
            return cards;
        }

        var testStrings = EnumerateTestStrings(test).ToList();
        var matchingRules = rules
            .Where(rule => rule != null && string.Equals(rule.TestType?.Trim(), testType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var rule in matchingRules)
        {
            if (!RuleMatches(rule, testStrings))
            {
                continue;
            }

            foreach (var card in rule.Cards ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(card))
                {
                    cards.Add(card.Trim());
                }
            }
        }

        if (cards.Count > 0)
        {
            return NormalizeCards(cards);
        }

        if (!string.IsNullOrWhiteSpace(fallbackCardName))
        {
            cards.Add(fallbackCardName);
        }

        return NormalizeCards(cards);
    }

    /// <summary>
    /// Executes RuleMatches.
    /// </summary>
    private static bool RuleMatches(TestTypeCardRule rule, List<string> testStrings)
    {
        if (string.IsNullOrWhiteSpace(rule.MatchRegex))
        {
            return true;
        }

        try
        {
            var regex = new Regex(rule.MatchRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return testStrings.Any(value => !string.IsNullOrWhiteSpace(value) && regex.IsMatch(value));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes NormalizeCards.
    /// </summary>
    private static List<string> NormalizeCards(IEnumerable<string> cards)
    {
        var result = new List<string>();
        foreach (var card in cards)
        {
            if (string.IsNullOrWhiteSpace(card))
            {
                continue;
            }

            var split = card.Split(new[] { ',', ';', '+', '/', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in split)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Executes EnumerateTests.
    /// </summary>
    private static IEnumerable<Test> EnumerateTests(Ct3xxProgram program)
    {
        foreach (var test in EnumerateTests(program.RootItems))
        {
            yield return test;
        }

        if (program.DutLoop?.Items != null)
        {
            foreach (var test in EnumerateTests(program.DutLoop.Items))
            {
                yield return test;
            }
        }
    }

    /// <summary>
    /// Executes EnumerateTests.
    /// </summary>
    private static IEnumerable<Test> EnumerateTests(IEnumerable<SequenceNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case Test test:
                    yield return test;
                    foreach (var child in EnumerateTests(test.Items))
                    {
                        yield return child;
                    }
                    break;
                case Ct3xxProgramParser.Model.Group group:
                    foreach (var child in EnumerateTests(group.Items))
                    {
                        yield return child;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Executes ExtractCardIndices.
    /// </summary>
    private static HashSet<int> ExtractCardIndices(
        string cardName,
        Test test,
        Dictionary<string, string> cardIndexPatterns)
    {
        var indices = new HashSet<int>();
        var regex = ResolveCardIndexRegex(cardName, cardIndexPatterns);
        if (regex == null)
        {
            return indices;
        }

        foreach (var value in EnumerateTestStrings(test))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (Match match in regex.Matches(value))
            {
                if (match.Groups.Count < 2)
                {
                    continue;
                }

                if (int.TryParse(match.Groups[1].Value, out var index))
                {
                    indices.Add(index);
                }
            }
        }

        return indices;
    }

    /// <summary>
    /// Executes ResolveCardIndexRegex.
    /// </summary>
    private static Regex? ResolveCardIndexRegex(string cardName, Dictionary<string, string> cardIndexPatterns)
    {
        if (cardIndexPatterns.TryGetValue(cardName, out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch
            {
                return null;
            }
        }

        if (string.Equals(cardName, "AM2", StringComparison.OrdinalIgnoreCase))
        {
            return new Regex(@"AM2\s*/\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        return null;
    }

    /// <summary>
    /// Executes EnumerateTestStrings.
    /// </summary>
    private static IEnumerable<string> EnumerateTestStrings(Test test)
    {
        if (!string.IsNullOrWhiteSpace(test.Id)) yield return test.Id!;
        if (!string.IsNullOrWhiteSpace(test.Name)) yield return test.Name!;
        if (!string.IsNullOrWhiteSpace(test.File)) yield return test.File!;

        var parameters = test.Parameters;
        if (parameters != null)
        {
            if (!string.IsNullOrWhiteSpace(parameters.Name)) yield return parameters.Name!;
            if (!string.IsNullOrWhiteSpace(parameters.DrawingReference)) yield return parameters.DrawingReference!;
            if (!string.IsNullOrWhiteSpace(parameters.Message)) yield return parameters.Message!;
            if (!string.IsNullOrWhiteSpace(parameters.Image)) yield return parameters.Image!;
            if (!string.IsNullOrWhiteSpace(parameters.Mode)) yield return parameters.Mode!;
            if (!string.IsNullOrWhiteSpace(parameters.Options)) yield return parameters.Options!;
            if (!string.IsNullOrWhiteSpace(parameters.OptionsVariable)) yield return parameters.OptionsVariable!;
            if (!string.IsNullOrWhiteSpace(parameters.Function)) yield return parameters.Function!;
            if (!string.IsNullOrWhiteSpace(parameters.Library)) yield return parameters.Library!;
            if (!string.IsNullOrWhiteSpace(parameters.ResultArray)) yield return parameters.ResultArray!;

            foreach (var record in parameters.Records)
            {
                foreach (var value in EnumerateRecordStrings(record))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
                }
            }

            foreach (var value in EnumerateAcqChannelStrings(parameters.AcquisitionChannel1))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }

            foreach (var value in EnumerateAcqChannelStrings(parameters.AcquisitionChannel2))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }

            foreach (var value in EnumerateAcqChannelStrings(parameters.AcquisitionChannel3))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }

            foreach (var value in EnumerateStimulusChannelStrings(parameters.StimulusChannel1))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }

            foreach (var value in EnumerateStimulusChannelStrings(parameters.StimulusChannel2))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }

    /// <summary>
    /// Executes EnumerateRecordStrings.
    /// </summary>
    private static IEnumerable<string> EnumerateRecordStrings(Record record)
    {
        if (!string.IsNullOrWhiteSpace(record.Id)) yield return record.Id!;
        if (!string.IsNullOrWhiteSpace(record.Variable)) yield return record.Variable!;
        if (!string.IsNullOrWhiteSpace(record.Text)) yield return record.Text!;
        if (!string.IsNullOrWhiteSpace(record.Destination)) yield return record.Destination!;
        if (!string.IsNullOrWhiteSpace(record.Expression)) yield return record.Expression!;
        if (!string.IsNullOrWhiteSpace(record.Type)) yield return record.Type!;
        if (!string.IsNullOrWhiteSpace(record.DrawingReference)) yield return record.DrawingReference!;
        if (!string.IsNullOrWhiteSpace(record.Unit)) yield return record.Unit!;
        if (!string.IsNullOrWhiteSpace(record.TestPoint)) yield return record.TestPoint!;
        if (!string.IsNullOrWhiteSpace(record.D)) yield return record.D!;
        if (!string.IsNullOrWhiteSpace(record.Voltage)) yield return record.Voltage!;
        if (!string.IsNullOrWhiteSpace(record.Time)) yield return record.Time!;
        if (!string.IsNullOrWhiteSpace(record.Resistance)) yield return record.Resistance!;
        if (!string.IsNullOrWhiteSpace(record.SwitchState)) yield return record.SwitchState!;
        if (!string.IsNullOrWhiteSpace(record.DestinationVariable)) yield return record.DestinationVariable!;
        if (!string.IsNullOrWhiteSpace(record.Device)) yield return record.Device!;
        if (!string.IsNullOrWhiteSpace(record.Control)) yield return record.Control!;
        if (!string.IsNullOrWhiteSpace(record.Direction)) yield return record.Direction!;
        if (!string.IsNullOrWhiteSpace(record.Command)) yield return record.Command!;
    }

    /// <summary>
    /// Executes EnumerateAcqChannelStrings.
    /// </summary>
    private static IEnumerable<string> EnumerateAcqChannelStrings(AcquisitionChannel? channel)
    {
        if (channel == null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(channel.Source)) yield return channel.Source!;
        if (!string.IsNullOrWhiteSpace(channel.Range)) yield return channel.Range!;
        if (!string.IsNullOrWhiteSpace(channel.Filter)) yield return channel.Filter!;
        if (!string.IsNullOrWhiteSpace(channel.UpperVoltageLimit)) yield return channel.UpperVoltageLimit!;
        if (!string.IsNullOrWhiteSpace(channel.LowerVoltageLimit)) yield return channel.LowerVoltageLimit!;
        if (channel.AdditionalAttributes != null)
        {
            foreach (var attr in channel.AdditionalAttributes)
            {
                if (!string.IsNullOrWhiteSpace(attr?.Value))
                {
                    yield return attr.Value;
                }
            }
        }
    }

    /// <summary>
    /// Executes EnumerateStimulusChannelStrings.
    /// </summary>
    private static IEnumerable<string> EnumerateStimulusChannelStrings(StimulusChannel? channel)
    {
        if (channel == null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(channel.Target)) yield return channel.Target!;
        if (!string.IsNullOrWhiteSpace(channel.Source)) yield return channel.Source!;
        if (!string.IsNullOrWhiteSpace(channel.Mode)) yield return channel.Mode!;
        if (!string.IsNullOrWhiteSpace(channel.Current)) yield return channel.Current!;
        if (!string.IsNullOrWhiteSpace(channel.Voltage)) yield return channel.Voltage!;
        if (!string.IsNullOrWhiteSpace(channel.VoltageLimit)) yield return channel.VoltageLimit!;
        if (!string.IsNullOrWhiteSpace(channel.Power)) yield return channel.Power!;
        if (!string.IsNullOrWhiteSpace(channel.Delay)) yield return channel.Delay!;
    }

    private sealed class RequiredCardUsage
    {
        /// <summary>
        /// Initializes a new instance of RequiredCardUsage.
        /// </summary>
        public RequiredCardUsage(string cardName)
        {
            CardName = cardName;
        }

        /// <summary>
        /// Gets or sets CardName.
        /// </summary>
        public string CardName { get; }
        /// <summary>
        /// Gets or sets RequiredCount.
        /// </summary>
        public int RequiredCount { get; private set; }
        private bool _hasTest;

        /// <summary>
        /// Executes MarkUsed.
        /// </summary>
        public void MarkUsed(string testType)
        {
            _hasTest = true;
        }

        /// <summary>
        /// Executes RegisterIndices.
        /// </summary>
        public void RegisterIndices(HashSet<int> indices)
        {
            if (indices.Count > 0)
            {
                var max = indices.Max();
                RequiredCount = Math.Max(RequiredCount, max);
            }
            else if (_hasTest && RequiredCount == 0)
            {
                RequiredCount = 1;
            }
        }
    }
}
