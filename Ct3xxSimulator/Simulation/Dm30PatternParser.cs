using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Parses DM30 digital pattern (.ctdig) files into a structured model.
/// </summary>
internal static class Dm30PatternParser
{
    private static readonly Encoding PatternEncoding = GetPatternEncoding();

    /// <summary>
    /// Executes TryParse.
    /// </summary>
    public static bool TryParse(string filePath, out Dm30PatternDocument document, out string? error)
    {
        document = Dm30PatternDocument.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            error = "Patterndatei fehlt.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            error = $"Patterndatei nicht gefunden: {Path.GetFileName(filePath)}";
            return false;
        }

        var lines = File.ReadAllLines(filePath, PatternEncoding);
        var parser = new Parser(lines);
        document = parser.Parse(filePath);
        error = parser.Error;
        return string.IsNullOrWhiteSpace(error);
    }

    /// <summary>
    /// Executes ParseHexToBits.
    /// </summary>
    public static List<int>? ParseHexToBits(string? hex, int expectedBits)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return expectedBits > 0 ? Enumerable.Repeat(0, expectedBits).ToList() : new List<int>();
        }

        var normalized = NormalizeHex(hex);
        if (normalized.Length == 0)
        {
            return null;
        }

        var bits = new List<int>(normalized.Length * 4);
        foreach (var character in normalized)
        {
            var value = int.Parse(character.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            bits.Add((value >> 3) & 0x1);
            bits.Add((value >> 2) & 0x1);
            bits.Add((value >> 1) & 0x1);
            bits.Add(value & 0x1);
        }

        if (expectedBits <= 0)
        {
            return bits;
        }

        if (bits.Count < expectedBits)
        {
            bits.AddRange(Enumerable.Repeat(0, expectedBits - bits.Count));
        }
        else if (bits.Count > expectedBits)
        {
            bits.RemoveRange(expectedBits, bits.Count - expectedBits);
        }

        return bits;
    }

    /// <summary>
    /// Executes NormalizeHex.
    /// </summary>
    private static string NormalizeHex(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            if (Uri.IsHexDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Executes GetPatternEncoding.
    /// </summary>
    private static Encoding GetPatternEncoding()
    {
        try
        {
            return Encoding.GetEncoding(1252);
        }
        catch (NotSupportedException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
    }

    private sealed class Parser
    {
        private readonly string[] _lines;
        private readonly Stack<string> _blockStack = new();
        private string? _pendingBlock;
        private Dm30Group? _currentGroup;
        private Dm30Signal? _currentSignal;
        private string? _testName;
        private int _testSteps;
        private int _testStart;
        private int _testEnd;
        private double _testFrequencyHz = 1000.0d;
        private readonly List<Dm30Group> _stimuli = new();
        private readonly List<Dm30Group> _acquisition = new();

        /// <summary>
        /// Initializes a new instance of Parser.
        /// </summary>
        public Parser(string[] lines)
        {
            _lines = lines;
        }

        /// <summary>
        /// Gets or sets Error.
        /// </summary>
        public string? Error { get; private set; }

        /// <summary>
        /// Executes Parse.
        /// </summary>
        public Dm30PatternDocument Parse(string filePath)
        {
            for (var index = 0; index < _lines.Length; index++)
            {
                var rawLine = _lines[index];
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line == "{")
                {
                    EnterPendingBlock();
                    continue;
                }

                if (line == "}")
                {
                    ExitBlock();
                    continue;
                }

                if (line.StartsWith("DM2TESTNAME", StringComparison.OrdinalIgnoreCase))
                {
                    _testName = ExtractValue(line);
                    continue;
                }

                if (line.StartsWith("TESTSTEPS", StringComparison.OrdinalIgnoreCase))
                {
                    _testSteps = ParseIntValue(line);
                    continue;
                }

                if (line.StartsWith("TESTSTART", StringComparison.OrdinalIgnoreCase))
                {
                    _testStart = ParseIntValue(line);
                    continue;
                }

                if (line.StartsWith("TESTEND", StringComparison.OrdinalIgnoreCase))
                {
                    _testEnd = ParseIntValue(line);
                    continue;
                }

                if (line.StartsWith("TESTFREQUENCY1", StringComparison.OrdinalIgnoreCase))
                {
                    _testFrequencyHz = ParseDoubleValue(line, _testFrequencyHz);
                    continue;
                }

                if (line.StartsWith("GROUPSTIMULI", StringComparison.OrdinalIgnoreCase))
                {
                    _pendingBlock = "GROUPSTIMULI";
                    continue;
                }

                if (line.StartsWith("GROUPACQUISITION", StringComparison.OrdinalIgnoreCase))
                {
                    _pendingBlock = "GROUPACQUISITION";
                    continue;
                }

                if (line.StartsWith("TRISTATESIGNAL", StringComparison.OrdinalIgnoreCase))
                {
                    _pendingBlock = "TRISTATESIGNAL";
                    continue;
                }

                if (line.StartsWith("SIGNAL", StringComparison.OrdinalIgnoreCase))
                {
                    _pendingBlock = "SIGNAL";
                    continue;
                }

                if (line.StartsWith("HLEVEL", StringComparison.OrdinalIgnoreCase))
                {
                    if (_currentGroup != null)
                    {
                        _currentGroup.HighLevel = ParseDoubleValue(line, _currentGroup.HighLevel);
                    }
                    continue;
                }

                if (line.StartsWith("LLEVEL", StringComparison.OrdinalIgnoreCase))
                {
                    if (_currentGroup != null)
                    {
                        _currentGroup.LowLevel = ParseDoubleValue(line, _currentGroup.LowLevel);
                    }
                    continue;
                }

                if (_currentSignal != null)
                {
                    if (line.StartsWith("NAME", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSignal.Name = ExtractValue(line) ?? _currentSignal.Name;
                        continue;
                    }

                    if (line.StartsWith("SIGNALUSED", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSignal.IsUsed = ParseIntValue(line) != 0;
                        continue;
                    }

                    if (line.StartsWith("STIMULIPATTERN", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSignal.StimuliPattern = ReadPatternBlock(ref index);
                        continue;
                    }

                    if (line.StartsWith("ACQUISITIONNOMINALPATTERN", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSignal.AcquisitionNominalPattern = ReadPatternBlock(ref index);
                        continue;
                    }

                    if (line.StartsWith("ACQUISITIONMASKPATTERN", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSignal.AcquisitionMaskPattern = ReadPatternBlock(ref index);
                        continue;
                    }
                }
            }

            return new Dm30PatternDocument(
                filePath,
                _testName ?? Path.GetFileNameWithoutExtension(filePath),
                _testSteps,
                _testStart,
                _testEnd,
                _testFrequencyHz,
                _stimuli,
                _acquisition);
        }

        /// <summary>
        /// Executes EnterPendingBlock.
        /// </summary>
        private void EnterPendingBlock()
        {
            if (string.IsNullOrWhiteSpace(_pendingBlock))
            {
                _blockStack.Push("UNKNOWN");
                return;
            }

            var block = _pendingBlock;
            _pendingBlock = null;
            _blockStack.Push(block);

            if (block.Equals("GROUPSTIMULI", StringComparison.OrdinalIgnoreCase))
            {
                _currentGroup = new Dm30Group("Stimuli");
                _stimuli.Add(_currentGroup);
                return;
            }

            if (block.Equals("GROUPACQUISITION", StringComparison.OrdinalIgnoreCase))
            {
                _currentGroup = new Dm30Group("Acquisition");
                _acquisition.Add(_currentGroup);
                return;
            }

            if (block.Equals("SIGNAL", StringComparison.OrdinalIgnoreCase) || block.Equals("TRISTATESIGNAL", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentGroup == null)
                {
                    Error = "Signalblock ohne Gruppe.";
                    return;
                }

                _currentSignal = new Dm30Signal(block.Equals("TRISTATESIGNAL", StringComparison.OrdinalIgnoreCase));
                _currentGroup.Signals.Add(_currentSignal);
            }
        }

        /// <summary>
        /// Executes ExitBlock.
        /// </summary>
        private void ExitBlock()
        {
            if (_blockStack.Count == 0)
            {
                return;
            }

            var block = _blockStack.Pop();
            if (block.Equals("SIGNAL", StringComparison.OrdinalIgnoreCase) ||
                block.Equals("TRISTATESIGNAL", StringComparison.OrdinalIgnoreCase))
            {
                _currentSignal = null;
            }

            if (block.Equals("GROUPSTIMULI", StringComparison.OrdinalIgnoreCase) ||
                block.Equals("GROUPACQUISITION", StringComparison.OrdinalIgnoreCase))
            {
                _currentGroup = null;
            }
        }

        /// <summary>
        /// Executes ReadPatternBlock.
        /// </summary>
        private string? ReadPatternBlock(ref int index)
        {
            for (var i = index + 1; i < _lines.Length; i++)
            {
                var line = _lines[i].Trim();
                if (line == "{")
                {
                    index = i;
                    break;
                }
            }

            var builder = new StringBuilder();
            for (var i = index + 1; i < _lines.Length; i++)
            {
                var line = _lines[i].Trim();
                if (line == "}")
                {
                    index = i;
                    break;
                }

                foreach (var character in line)
                {
                    if (Uri.IsHexDigit(character))
                    {
                        builder.Append(char.ToUpperInvariant(character));
                    }
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        /// <summary>
        /// Executes ExtractValue.
        /// </summary>
        private static string? ExtractValue(string line)
        {
            var atIndex = line.IndexOf('@');
            if (atIndex < 0)
            {
                return null;
            }

            var endIndex = line.IndexOf('@', atIndex + 1);
            if (endIndex < 0 || endIndex <= atIndex)
            {
                return null;
            }

            var value = line[(atIndex + 1)..endIndex];
            return value.Trim();
        }

        /// <summary>
        /// Executes ParseIntValue.
        /// </summary>
        private static int ParseIntValue(string line)
        {
            var value = ExtractValue(line);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        /// <summary>
        /// Executes ParseDoubleValue.
        /// </summary>
        private static double ParseDoubleValue(string line, double fallback)
        {
            var value = ExtractValue(line);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            value = value.Replace(',', '.');
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }
    }
}

internal sealed record Dm30PatternDocument(
    string FilePath,
    string TestName,
    int TestSteps,
    int TestStart,
    int TestEnd,
    double TestFrequencyHz,
    IReadOnlyList<Dm30Group> StimuliGroups,
    IReadOnlyList<Dm30Group> AcquisitionGroups)
{
    public static readonly Dm30PatternDocument Empty = new(
        string.Empty,
        string.Empty,
        0,
        0,
        0,
        0d,
        Array.Empty<Dm30Group>(),
        Array.Empty<Dm30Group>());
}

internal sealed class Dm30Group
{
    /// <summary>
    /// Initializes a new instance of Dm30Group.
    /// </summary>
    public Dm30Group(string kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Gets or sets Kind.
    /// </summary>
    public string Kind { get; }
    /// <summary>
    /// Gets or sets HighLevel.
    /// </summary>
    public double HighLevel { get; set; } = 0d;
    /// <summary>
    /// Gets or sets LowLevel.
    /// </summary>
    public double LowLevel { get; set; } = 0d;
    /// <summary>
    /// Gets or sets Signals.
    /// </summary>
    public List<Dm30Signal> Signals { get; } = new();
}

internal sealed class Dm30Signal
{
    /// <summary>
    /// Initializes a new instance of Dm30Signal.
    /// </summary>
    public Dm30Signal(bool isTriState)
    {
        IsTriState = isTriState;
    }

    /// <summary>
    /// Gets or sets Name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets IsUsed.
    /// </summary>
    public bool IsUsed { get; set; } = true;
    /// <summary>
    /// Gets or sets IsTriState.
    /// </summary>
    public bool IsTriState { get; }
    /// <summary>
    /// Gets or sets StimuliPattern.
    /// </summary>
    public string? StimuliPattern { get; set; }
    /// <summary>
    /// Gets or sets AcquisitionNominalPattern.
    /// </summary>
    public string? AcquisitionNominalPattern { get; set; }
    /// <summary>
    /// Gets or sets AcquisitionMaskPattern.
    /// </summary>
    public string? AcquisitionMaskPattern { get; set; }
}
