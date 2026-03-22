using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Ct3xxWireVizParser.Model;

public sealed class WireVizValue
{
    private static readonly IReadOnlyDictionary<string, WireVizValue> EmptyMap =
        new ReadOnlyDictionary<string, WireVizValue>(new Dictionary<string, WireVizValue>(StringComparer.Ordinal));

    private static readonly IReadOnlyList<WireVizValue> EmptyList = Array.Empty<WireVizValue>();

    private WireVizValue(
        WireVizValueKind kind,
        object? scalar,
        IReadOnlyDictionary<string, WireVizValue>? properties,
        IReadOnlyList<WireVizValue>? items)
    {
        Kind = kind;
        Scalar = scalar;
        Properties = properties ?? EmptyMap;
        Items = items ?? EmptyList;
    }

    public WireVizValueKind Kind { get; }
    public object? Scalar { get; }
    public IReadOnlyDictionary<string, WireVizValue> Properties { get; }
    public IReadOnlyList<WireVizValue> Items { get; }

    public static WireVizValue FromObject(object? value)
    {
        if (value == null)
        {
            return new WireVizValue(WireVizValueKind.Null, null, null, null);
        }

        if (value is IDictionary dictionary)
        {
            var properties = new Dictionary<string, WireVizValue>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                properties[key] = FromObject(entry.Value);
            }

            return new WireVizValue(
                WireVizValueKind.Mapping,
                null,
                new ReadOnlyDictionary<string, WireVizValue>(properties),
                null);
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var items = new List<WireVizValue>();
            foreach (var item in enumerable)
            {
                items.Add(FromObject(item));
            }

            return new WireVizValue(WireVizValueKind.Sequence, null, null, items.AsReadOnly());
        }

        return new WireVizValue(WireVizValueKind.Scalar, value, null, null);
    }

    public string? AsString()
    {
        if (Kind == WireVizValueKind.Null)
        {
            return null;
        }

        if (Kind != WireVizValueKind.Scalar)
        {
            return ToString();
        }

        return Scalar switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Scalar.ToString()
        };
    }

    public bool TryGetProperty(string name, out WireVizValue value)
    {
        if (Kind == WireVizValueKind.Mapping && Properties.TryGetValue(name, out value!))
        {
            return true;
        }

        value = null!;
        return false;
    }

    public IReadOnlyDictionary<string, WireVizValue> AsMappingOrEmpty() =>
        Kind == WireVizValueKind.Mapping ? Properties : EmptyMap;

    public IReadOnlyList<WireVizValue> AsSequenceOrEmpty() =>
        Kind == WireVizValueKind.Sequence ? Items : EmptyList;

    public override string ToString()
    {
        return Kind switch
        {
            WireVizValueKind.Null => "null",
            WireVizValueKind.Scalar => AsString() ?? string.Empty,
            WireVizValueKind.Mapping => $"{{{string.Join(", ", Properties.Keys)}}}",
            WireVizValueKind.Sequence => $"[{string.Join(", ", Items.Select(item => item.ToString()))}]",
            _ => string.Empty
        };
    }
}
