using System;
using System.Collections.Generic;
using System.Globalization;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

public class SimulationContext
{
    private readonly Dictionary<string, object?> _scalars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProgramArray> _arrays = new(StringComparer.OrdinalIgnoreCase);

    public string LastResult { get; private set; } = "PASS";

    public void ApplyTables(IEnumerable<Table> tables, ExpressionEvaluator evaluator)
    {
        foreach (var table in tables)
        {
            ApplyTable(table, evaluator);
        }
    }

    public void ApplyTable(Table table, ExpressionEvaluator evaluator)
    {
        if (table.Variables.Count == 0)
        {
            return;
        }

        foreach (var variable in table.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                continue;
            }

            var address = VariableAddress.From(variable.Name);
            var value = evaluator.Evaluate(variable.Initial);
            ApplyValue(address, value);
        }
    }

    public void SetValue(string name, object? value)
    {
        if (!VariableAddress.TryParse(name, out var address))
        {
            address = new VariableAddress(name.Trim(), null);
        }

        ApplyValue(address, value);
    }

    public void SetValue(VariableAddress address, object? value) => ApplyValue(address, value);

    private void ApplyValue(VariableAddress address, object? value)
    {
        if (value is ArrayAllocation allocation)
        {
            var array = GetOrCreateArray(address.Name);
            array.Reset(allocation.Length);
            return;
        }

        if (address.HasIndex)
        {
            var array = GetOrCreateArray(address.Name);
            array.Set(address.Index!.Value, value);
        }
        else
        {
            _scalars[address.Name] = value;
        }
    }

    private ProgramArray GetOrCreateArray(string name)
    {
        if (!_arrays.TryGetValue(name, out var array))
        {
            array = new ProgramArray();
            _arrays[name] = array;
        }

        return array;
    }

    public object? GetValue(VariableAddress address)
    {
        if (address.HasIndex)
        {
            return _arrays.TryGetValue(address.Name, out var array)
                ? array.Get(address.Index!.Value)
                : null;
        }

        return _scalars.TryGetValue(address.Name, out var value) ? value : null;
    }

    public object? GetValue(string? name)
    {
        if (!VariableAddress.TryParse(name, out var address))
        {
            return null;
        }

        return GetValue(address);
    }

    public ProgramArray? GetArray(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _arrays.TryGetValue(name.Trim(), out var array) ? array : null;
    }

    public void MarkOutcome(TestOutcome outcome)
    {
        LastResult = outcome switch
        {
            TestOutcome.Pass => "PASS",
            TestOutcome.Fail => "FAIL",
            TestOutcome.Error => "ERROR",
            _ => LastResult
        };

        _scalars["$Result"] = LastResult;
        _scalars["$DUTResult"] = LastResult;
    }

    public string Describe(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool b => b ? "TRUE" : "FALSE",
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}

public class ProgramArray
{
    private readonly SortedDictionary<int, object?> _items = new();

    public void Reset(int length)
    {
        _items.Clear();
        for (var i = 1; i <= length; i++)
        {
            _items[i] = null;
        }
    }

    public void Set(int index, object? value) => _items[index] = value;

    public object? Get(int index) => _items.TryGetValue(index, out var value) ? value : null;

    public IReadOnlyDictionary<int, object?> Snapshot() => _items;
}

public sealed class ArrayAllocation
{
    public ArrayAllocation(int length)
    {
        Length = length;
    }

    public int Length { get; }
}
