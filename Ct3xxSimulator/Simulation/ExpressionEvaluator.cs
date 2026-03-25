using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ct3xxSimulator.Simulation;

public class ExpressionEvaluator
{
    private readonly SimulationContext _context;

    public ExpressionEvaluator(SimulationContext context)
    {
        _context = context;
    }

    public object? Evaluate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var trimmed = expression.Trim();

        if (trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (trimmed.StartsWith("Dim(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            var inner = trimmed[4..^1];
            if (int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length))
            {
                return new ArrayAllocation(length);
            }
        }

        if (trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        var concatenation = SplitTopLevel(trimmed, '&');
        if (concatenation.Count > 1)
        {
            var builder = new StringBuilder();
            foreach (var part in concatenation)
            {
                builder.Append(ToText(Evaluate(part)));
            }

            return builder.ToString();
        }

        if (TryEvaluateFunction(trimmed, out var functionResult))
        {
            return functionResult;
        }

        if (VariableAddress.TryParse(trimmed, out var address))
        {
            return _context.GetValue(address);
        }

        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 3 && (tokens[1] == "+" || tokens[1] == "-"))
        {
            var left = ToDouble(Evaluate(tokens[0]));
            var right = ToDouble(Evaluate(tokens[2]));
            if (left.HasValue && right.HasValue)
            {
                return tokens[1] == "+" ? left.Value + right.Value : left.Value - right.Value;
            }
        }

        return trimmed;
    }

    public bool EvaluateCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var trimmed = condition.Trim();

        if (trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var operators = new[] { ">=", "<=", "==", "!=", ">", "<" };
        foreach (var op in operators)
        {
            var opIndex = trimmed.IndexOf(op, StringComparison.Ordinal);
            if (opIndex < 0)
            {
                continue;
            }

            var left = trimmed[..opIndex].Trim();
            var right = trimmed[(opIndex + op.Length)..].Trim();
            var leftValue = Evaluate(left);
            var rightValue = Evaluate(right);

            if (op is "==" or "!=")
            {
                var leftText = leftValue?.ToString() ?? string.Empty;
                var rightText = rightValue?.ToString() ?? TrimQuotes(right);
                return op == "=="
                    ? string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase)
                    : !string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            }

            var leftNumber = ToDouble(leftValue);
            var rightNumber = ToDouble(rightValue);
            if (!leftNumber.HasValue || !rightNumber.HasValue)
            {
                return true;
            }

            return op switch
            {
                ">" => leftNumber.Value > rightNumber.Value,
                "<" => leftNumber.Value < rightNumber.Value,
                ">=" => leftNumber.Value >= rightNumber.Value,
                "<=" => leftNumber.Value <= rightNumber.Value,
                _ => true
            };
        }

        var value = Evaluate(trimmed);
        return ToBool(value);
    }

    public string ResolveText(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return ToText(Evaluate(template.Trim()));
    }

    public IReadOnlyList<string> ParseOptions(string? options)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            return Array.Empty<string>();
        }

        var trimmed = options.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        var entries = trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => TrimQuotes(part.Trim()))
            .Where(part => part.Length > 0)
            .ToList();

        return entries;
    }

    public double? ToDouble(object? value)
    {
        return value switch
        {
            null => null,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            bool b => b ? 1d : 0d,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public bool ToBool(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s when s.Equals("TRUE", StringComparison.OrdinalIgnoreCase) => true,
            string s when s.Equals("FALSE", StringComparison.OrdinalIgnoreCase) => false,
            string s when s.Equals("PASS", StringComparison.OrdinalIgnoreCase) => true,
            string s when s.Equals("FAIL", StringComparison.OrdinalIgnoreCase) => false,
            _ => ToDouble(value).GetValueOrDefault() != 0
        };
    }

    public string ToText(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "TRUE" : "FALSE",
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? string.Empty
    };

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2 && ((value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal)) ||
                                  (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))))
        {
            return value[1..^1];
        }

        return value;
    }

    private bool TryEvaluateFunction(string expression, out object? result)
    {
        result = null;
        var openParenIndex = expression.IndexOf('(');
        if (openParenIndex <= 0 || !expression.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var functionName = expression[..openParenIndex].Trim();
        var argumentContent = expression[(openParenIndex + 1)..^1];
        var arguments = SplitArguments(argumentContent);

        switch (functionName.ToUpperInvariant())
        {
            case "TESTPROGRAMPATH":
                result = _context.ProgramDirectory;
                return true;

            case "PATHCOMBINE":
                result = Path.Combine(arguments.Select(argument => ToText(Evaluate(argument))).ToArray());
                return true;

            case "CHAR":
                if (arguments.Count == 1 && ToDouble(Evaluate(arguments[0])) is double charValue)
                {
                    result = ((char)charValue).ToString();
                    return true;
                }

                return false;

            case "WRITEFILE":
                result = ExecuteWriteFile(arguments);
                return true;

            default:
                return false;
        }
    }

    private object ExecuteWriteFile(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2)
        {
            return 0d;
        }

        var path = ToText(Evaluate(arguments[0]));
        var content = ToText(Evaluate(arguments[1]));
        var mode = arguments.Count > 2 ? ToText(Evaluate(arguments[2])).Trim().Trim('\'', '"') : "c";
        if (string.IsNullOrWhiteSpace(path))
        {
            return 0d;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (mode.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            File.AppendAllText(fullPath, content, Encoding.UTF8);
        }
        else
        {
            File.WriteAllText(fullPath, content, Encoding.UTF8);
        }

        return new FileInfo(fullPath).Length;
    }

    private static List<string> SplitArguments(string text)
    {
        return SplitTopLevel(text, ',');
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var start = 0;
        var depth = 0;
        var inSingleQuotes = false;
        var inDoubleQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                continue;
            }

            if (ch == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                continue;
            }

            if (inSingleQuotes || inDoubleQuotes)
            {
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')')
            {
                depth--;
                continue;
            }

            if (ch != separator || depth != 0)
            {
                continue;
            }

            var part = text[start..i].Trim();
            if (part.Length > 0)
            {
                result.Add(part);
            }

            start = i + 1;
        }

        var last = text[start..].Trim();
        if (last.Length > 0)
        {
            result.Add(last);
        }

        return result;
    }
}
