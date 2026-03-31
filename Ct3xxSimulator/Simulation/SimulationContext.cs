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
    private readonly List<SimulationScope> _scopes = new();

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

    private SimulationScope CurrentScope => _scopes[^1];
    private SimulationScope GlobalScope => _scopes[0];

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationContext"/> class.
    /// </summary>
    public SimulationContext()
    {
        _scopes.Add(new SimulationScope());
        DefineImplicit("$Result");
        DefineImplicit("$DUTResult");
        DefineImplicit("$TestProgramPath");
        DefineImplicit("$TestProgramFile");
        DefineImplicit("$LoopCounter");
        DefineImplicit("$CommandEnd");
        DefineImplicit("$LoggingPrefix");
        DefineImplicit("$LoggingSuffix");
        DefineImplicit("$BoardIndex");
    }

    /// <summary>
    /// Updates the active program file and directory context.
    /// </summary>
    public void SetProgramContext(string? programPath)
    {
        ProgramPath = string.IsNullOrWhiteSpace(programPath) ? null : Path.GetFullPath(programPath);
        ProgramDirectory = ProgramPath == null
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(ProgramPath) ?? Directory.GetCurrentDirectory();

        SetValue("$TestProgramPath", ProgramDirectory);
        SetValue("$TestProgramFile", ProgramPath ?? string.Empty);
    }

    /// <summary>
    /// Pushes a new variable scope.
    /// </summary>
    public IDisposable PushScope()
    {
        _scopes.Add(new SimulationScope());
        return new ScopeHandle(this);
    }

    /// <summary>
    /// Pops the current variable scope.
    /// </summary>
    public void PopScope()
    {
        if (_scopes.Count > 1)
        {
            _scopes.RemoveAt(_scopes.Count - 1);
        }
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

            DefineVariable(variable.Name);
            var address = VariableAddress.From(variable.Name);
            var value = evaluator.Evaluate(variable.Initial);
            ApplyValue(address, value, CurrentScope);
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

        var scope = ResolveScope(address.Name);
        if (scope == null)
        {
            if (IsImplicitVariable(address.Name))
            {
                DefineImplicit(address.Name);
                scope = GlobalScope;
            }
            else
            {
                throw new UndefinedVariableException(address.Name);
            }
        }

        ApplyValue(address, value, scope);
    }

    /// <summary>
    /// Sets one scalar or indexed variable value by parsed address.
    /// </summary>
    public void SetValue(VariableAddress address, object? value)
    {
        var scope = ResolveScope(address.Name);
        if (scope == null)
        {
            if (IsImplicitVariable(address.Name))
            {
                DefineImplicit(address.Name);
                scope = GlobalScope;
            }
            else
            {
                throw new UndefinedVariableException(address.Name);
            }
        }

        ApplyValue(address, value, scope);
    }

    /// <summary>
    /// Sets one scalar or indexed variable value in the nearest outer scope.
    /// </summary>
    public void SetValueInOuterScope(string name, object? value)
    {
        if (!VariableAddress.TryParse(name, out var address))
        {
            address = new VariableAddress(name.Trim(), null);
        }

        var scope = ResolveOuterScope(address.Name);
        if (scope == null)
        {
            if (IsImplicitVariable(address.Name))
            {
                DefineImplicit(address.Name);
                scope = GlobalScope;
            }
            else
            {
                throw new UndefinedVariableException(address.Name);
            }
        }

        ApplyValue(address, value, scope);
    }

    /// <summary>
    /// Determines whether a variable is defined in the current scope.
    /// </summary>
    public bool IsDefinedInCurrentScope(string name) =>
        !string.IsNullOrWhiteSpace(name) && CurrentScope.Definitions.Contains(name.Trim());

    /// <summary>
    /// Determines whether a variable is defined in any outer scope.
    /// </summary>
    public bool IsDefinedInOuterScope(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (var index = _scopes.Count - 2; index >= 0; index--)
        {
            if (_scopes[index].Definitions.Contains(name.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a variable is defined in any scope.
    /// </summary>
    public bool IsDefined(VariableAddress address) => ResolveScope(address.Name) != null;

    private SimulationScope? ResolveScope(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        for (var index = _scopes.Count - 1; index >= 0; index--)
        {
            if (_scopes[index].Definitions.Contains(name.Trim()))
            {
                return _scopes[index];
            }
        }

        return null;
    }

    private SimulationScope? ResolveOuterScope(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        for (var index = _scopes.Count - 2; index >= 0; index--)
        {
            if (_scopes[index].Definitions.Contains(name.Trim()))
            {
                return _scopes[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Executes ApplyValue.
    /// </summary>
    private void ApplyValue(VariableAddress address, object? value, SimulationScope scope)
    {
        if (value is ArrayAllocation allocation)
        {
            var array = scope.GetOrCreateArray(address.Name);
            array.Reset(allocation.Length);
            return;
        }

        if (address.HasIndex)
        {
            var array = scope.GetOrCreateArray(address.Name);
            array.Set(address.Index!.Value, value);
        }
        else
        {
            scope.Scalars[address.Name] = value;
        }
    }

    /// <summary>
    /// Executes GetOrCreateArray.
    /// </summary>
    private static bool IsImplicitVariable(string name) =>
        name.StartsWith("$", StringComparison.Ordinal);

    private void DefineVariable(string name) => CurrentScope.Definitions.Add(name.Trim());

    private void DefineImplicit(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            GlobalScope.Definitions.Add(name.Trim());
        }
    }

    /// <summary>
    /// Reads one scalar or indexed variable value by parsed address.
    /// </summary>
    public object? GetValue(VariableAddress address)
    {
        var scope = ResolveScope(address.Name);
        if (scope == null)
        {
            return null;
        }

        if (address.HasIndex)
        {
            return scope.Arrays.TryGetValue(address.Name, out var array)
                ? array.Get(address.Index!.Value)
                : null;
        }

        return scope.Scalars.TryGetValue(address.Name, out var value) ? value : null;
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

        var scope = ResolveScope(name.Trim());
        return scope != null && scope.Arrays.TryGetValue(name.Trim(), out var array) ? array : null;
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

        SetValue("$Result", LastResult);
        SetValue("$DUTResult", LastResult);
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

internal sealed class SimulationScope
{
    public Dictionary<string, object?> Scalars { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ProgramArray> Arrays { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Definitions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ProgramArray GetOrCreateArray(string name)
    {
        if (!Arrays.TryGetValue(name, out var array))
        {
            array = new ProgramArray();
            Arrays[name] = array;
        }

        return array;
    }
}

internal sealed class ScopeHandle : IDisposable
{
    private readonly SimulationContext _context;
    private bool _disposed;

    public ScopeHandle(SimulationContext context)
    {
        _context = context;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _context.PopScope();
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
