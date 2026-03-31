using System;
using System.Collections.Generic;
using System.Globalization;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Stores CT3xx variables, arrays and run-level state for one simulation session.
/// </summary>
public class SimulationContext
{
    private readonly Dictionary<string, object?> _scalars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProgramArray> _arrays = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the textual result of the last completed test or step.
    /// </summary>
    public string LastResult { get; private set; } = "PASS";
    /// <summary>
    /// Gets the active CT3xx program directory.
    /// </summary>
    public string ProgramDirectory { get; private set; } = Directory.GetCurrentDirectory();
    /// <summary>
    /// Gets the active CT3xx program file path, if known.
    /// </summary>
    public string? ProgramPath { get; private set; }

    /// <summary>
    /// Updates the active program file and directory context.
    /// </summary>
    public void SetProgramContext(string? programPath)
    {
        ProgramPath = string.IsNullOrWhiteSpace(programPath) ? null : Path.GetFullPath(programPath);
        ProgramDirectory = ProgramPath == null
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(ProgramPath) ?? Directory.GetCurrentDirectory();

        _scalars["$TestProgramPath"] = ProgramDirectory;
        _scalars["$TestProgramFile"] = ProgramPath ?? string.Empty;
    }

    /// <summary>
    /// Applies all variable tables to the context.
    /// </summary>
    public void ApplyTables(IEnumerable<Table> tables, ExpressionEvaluator evaluator)
    {
        foreach (var table in tables)
        {
            ApplyTable(table, evaluator);
        }
    }

    /// <summary>
    /// Applies one variable table to the context.
    /// </summary>
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

    /// <summary>
    /// Sets one scalar or indexed variable value by name.
    /// </summary>
    public void SetValue(string name, object? value)
    {
        if (!VariableAddress.TryParse(name, out var address))
        {
            address = new VariableAddress(name.Trim(), null);
        }

        ApplyValue(address, value);
    }

    /// <summary>
    /// Sets one scalar or indexed variable value by parsed address.
    /// </summary>
    public void SetValue(VariableAddress address, object? value) => ApplyValue(address, value);

    /// <summary>
    /// Executes ApplyValue.
    /// </summary>
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

    /// <summary>
    /// Executes GetOrCreateArray.
    /// </summary>
    private ProgramArray GetOrCreateArray(string name)
    {
        if (!_arrays.TryGetValue(name, out var array))
        {
            array = new ProgramArray();
            _arrays[name] = array;
        }

        return array;
    }

    /// <summary>
    /// Reads one scalar or indexed variable value by parsed address.
    /// </summary>
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

    /// <summary>
    /// Reads one scalar or indexed variable value by its textual name.
    /// </summary>
    public object? GetValue(string? name)
    {
        if (!VariableAddress.TryParse(name, out var address))
        {
            return null;
        }

        return GetValue(address);
    }

    /// <summary>
    /// Returns one array by name when it exists.
    /// </summary>
    public ProgramArray? GetArray(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _arrays.TryGetValue(name.Trim(), out var array) ? array : null;
    }

    /// <summary>
    /// Updates the cached textual result variables from a normalized outcome.
    /// </summary>
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

    /// <summary>
    /// Converts one value into a user-facing CT3xx-style text representation.
    /// </summary>
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

/// <summary>
/// Stores one one-based CT3xx array.
/// </summary>
public class ProgramArray
{
    private readonly SortedDictionary<int, object?> _items = new();

    /// <summary>
    /// Resets the array to the specified one-based length.
    /// </summary>
    public void Reset(int length)
    {
        _items.Clear();
        for (var i = 1; i <= length; i++)
        {
            _items[i] = null;
        }
    }

    /// <summary>
    /// Stores one array item at the specified one-based index.
    /// </summary>
    public void Set(int index, object? value) => _items[index] = value;

    /// <summary>
    /// Reads one array item at the specified one-based index.
    /// </summary>
    public object? Get(int index) => _items.TryGetValue(index, out var value) ? value : null;

    /// <summary>
    /// Returns an immutable view of the current array content.
    /// </summary>
    public IReadOnlyDictionary<int, object?> Snapshot() => _items;
}

/// <summary>
/// Represents a deferred CT3xx array allocation request such as <c>Dim(10)</c>.
/// </summary>
public sealed class ArrayAllocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayAllocation"/> class.
    /// </summary>
    /// <param name="length">The requested one-based array length.</param>
    public ArrayAllocation(int length)
    {
        Length = length;
    }

    /// <summary>
    /// Gets the requested one-based array length.
    /// </summary>
    public int Length { get; }
}
