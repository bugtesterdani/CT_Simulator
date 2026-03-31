using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ct3xxProgramParser.Discovery;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;

namespace Ct3xxSimulator.Simulation;

public partial class Ct3xxProgramSimulator
{
    private TestOutcome RunFunctionCallTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "FCA^ ohne Parameter.");
            return TestOutcome.Error;
        }

        var functionName = parameters.Function?.Trim();
        if (string.IsNullOrWhiteSpace(functionName))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "FCA^ ohne Function.");
            return TestOutcome.Error;
        }

        var libraryName = NormalizeLibraryName(parameters.Library);
        if (!IsLocalFunctionLibrary(libraryName))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"FCA^ nur fuer <Use local function group> implementiert ({libraryName}).");
            return TestOutcome.Error;
        }

        var inputRecords = CollectCallAssignments(parameters.Tables, "CAL$");
        var outputRecords = CollectCallAssignments(parameters.Tables, "RET$");
        var inputValues = EvaluateCallInputs(inputRecords);

        var localFunction = FindLocalFunction(functionName);
        if (localFunction != null)
        {
            var outcome = ExecuteLocalFunction(localFunction, inputValues, outputRecords, out var details);
            PublishStepEvaluation(test, outcome, details: details);
            return outcome;
        }

        if (TryExecuteProgramCall(functionName, inputValues, outputRecords, out var programOutcome, out var programDetails))
        {
            PublishStepEvaluation(test, programOutcome, details: programDetails);
            return programOutcome;
        }

        PublishStepEvaluation(test, TestOutcome.Error, details: $"FCA^ Funktion nicht gefunden: {functionName}");
        return TestOutcome.Error;
    }

    private TestOutcome ExecuteLocalFunction(
        LibraryFunction function,
        IReadOnlyList<CallAssignmentValue> inputs,
        IReadOnlyList<CallAssignment> outputs,
        out string details)
    {
        details = $"Function Call: {function.Name}";
        if (!string.IsNullOrWhiteSpace(function.ExecCondition))
        {
            EnsureConditionContext(function.ExecCondition);
            if (!_evaluator.EvaluateCondition(function.ExecCondition))
            {
                details = $"Function Call: {function.Name} (condition FALSE)";
                return TestOutcome.Pass;
            }
        }

        using var scope = _context.PushScope();

        foreach (var table in function.Tables)
        {
            _context.ApplyTable(table, _evaluator);
        }

        foreach (var input in inputs)
        {
            if (!_context.IsDefinedInCurrentScope(input.Destination))
            {
                throw new UndefinedVariableException(input.Destination);
            }

            _context.SetValue(input.Destination, input.Value);
        }

        ExecuteSequenceItems(function.Items);

        foreach (var output in outputs)
        {
            if (!_context.IsDefinedInOuterScope(output.Destination))
            {
                throw new UndefinedVariableException(output.Destination);
            }

            var value = _evaluator.Evaluate(output.Expression);
            _context.SetValueInOuterScope(output.Destination, value);
        }

        return ResolveResultOutcome();
    }

    private bool TryExecuteProgramCall(
        string functionName,
        IReadOnlyList<CallAssignmentValue> inputs,
        IReadOnlyList<CallAssignment> outputs,
        out TestOutcome outcome,
        out string details)
    {
        outcome = TestOutcome.Error;
        details = $"FCA^ Programm nicht gefunden: {functionName}";

        var programPath = ResolveProgramPath(functionName);
        if (programPath == null)
        {
            return false;
        }

        var parser = new Ct3xxProgramFileParser();
        var fileSet = parser.Load(programPath);
        var previousFileSet = _fileSet;
        var previousProgram = _program;
        var previousProgramPath = _context.ProgramPath;

        try
        {
            _fileSet = fileSet;
            _program = fileSet.Program;
            _context.SetProgramContext(fileSet.ProgramPath);

            using var scope = _context.PushScope();
            _context.ApplyTables(fileSet.Program.Tables, _evaluator);

            foreach (var input in inputs)
            {
                if (!_context.IsDefinedInCurrentScope(input.Destination))
                {
                    throw new UndefinedVariableException(input.Destination);
                }

                _context.SetValue(input.Destination, input.Value);
            }

            ExecuteProgramItems(fileSet.Program);

            foreach (var output in outputs)
            {
                if (!_context.IsDefinedInOuterScope(output.Destination))
                {
                    throw new UndefinedVariableException(output.Destination);
                }

                var value = _evaluator.Evaluate(output.Expression);
                _context.SetValueInOuterScope(output.Destination, value);
            }

            outcome = ResolveResultOutcome();
            details = $"Function Call: {functionName}";
            return true;
        }
        catch (UndefinedVariableException ex)
        {
            outcome = TestOutcome.Error;
            details = ex.Message;
            return true;
        }
        finally
        {
            _fileSet = previousFileSet;
            _program = previousProgram;
            _context.SetProgramContext(previousProgramPath);
        }
    }

    private void ExecuteProgramItems(Ct3xxProgram program)
    {
        var loopExecuted = false;
        foreach (var item in program.RootItems)
        {
            if (!loopExecuted &&
                program.DutLoop != null &&
                item is Group marker &&
                string.Equals(marker.Id, "END$", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteDutLoop(program.DutLoop, 1);
                loopExecuted = true;
            }

            switch (item)
            {
                case Table table:
                    try
                    {
                        _context.ApplyTable(table, _evaluator);
                    }
                    catch (UndefinedVariableException ex)
                    {
                        _observer.OnMessage($"Variablenfehler in Tabelle {table.Id}: {ex.Message}");
                        _context.MarkOutcome(TestOutcome.Error);
                    }
                    break;
                case Test test:
                    ExecuteTest(test);
                    break;
                case Group group:
                    ExecuteGroup(group);
                    break;
            }
        }

        if (!loopExecuted && program.DutLoop != null)
        {
            ExecuteDutLoop(program.DutLoop, 1);
        }
    }

    private LibraryFunction? FindLocalFunction(string functionName)
    {
        var table = FindLocalFunctionTable();
        if (table == null)
        {
            return null;
        }

        return table.Functions.FirstOrDefault(function =>
            string.Equals(function.Name, functionName, StringComparison.OrdinalIgnoreCase));
    }

    private Table? FindLocalFunctionTable()
    {
        if (_program == null)
        {
            return null;
        }

        for (var index = _groupStack.Count - 1; index >= 0; index--)
        {
            var group = _groupStack[index];
            var local = group.Items
                .OfType<Table>()
                .FirstOrDefault(table => string.Equals(table.Id, "FCT$", StringComparison.OrdinalIgnoreCase));
            if (local != null)
            {
                return local;
            }
        }

        var rootLocal = _program.RootItems
            .OfType<Table>()
            .FirstOrDefault(table => string.Equals(table.Id, "FCT$", StringComparison.OrdinalIgnoreCase));
        if (rootLocal != null)
        {
            return rootLocal;
        }

        return _program.Tables.FirstOrDefault(table => string.Equals(table.Id, "FCT$", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<CallAssignment> CollectCallAssignments(IEnumerable<Table> tables, string tableId)
    {
        if (tables == null)
        {
            return Array.Empty<CallAssignment>();
        }

        var assignments = new List<CallAssignment>();
        foreach (var table in tables)
        {
            if (!string.Equals(table.Id, tableId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var record in table.Records)
            {
                if (string.IsNullOrWhiteSpace(record.Destination) ||
                    string.IsNullOrWhiteSpace(record.Expression) ||
                    IsDisabled(record.Disabled))
                {
                    continue;
                }

                assignments.Add(new CallAssignment(record.Destination!, record.Expression!));
            }
        }

        return assignments;
    }

    private IReadOnlyList<CallAssignmentValue> EvaluateCallInputs(IReadOnlyList<CallAssignment> inputs)
    {
        if (inputs.Count == 0)
        {
            return Array.Empty<CallAssignmentValue>();
        }

        var values = new List<CallAssignmentValue>(inputs.Count);
        foreach (var input in inputs)
        {
            var value = _evaluator.Evaluate(input.Expression);
            values.Add(new CallAssignmentValue(input.Destination, value));
        }

        return values;
    }

    private TestOutcome ResolveResultOutcome()
    {
        var result = _evaluator.ToText(_context.GetValue("$Result"));
        return result.ToUpperInvariant() switch
        {
            "PASS" => TestOutcome.Pass,
            "FAIL" => TestOutcome.Fail,
            "ERROR" => TestOutcome.Error,
            "NORESULT" => TestOutcome.Pass,
            _ => _context.LastResult == "FAIL" ? TestOutcome.Fail :
                _context.LastResult == "ERROR" ? TestOutcome.Error : TestOutcome.Pass
        };
    }

    private static bool IsLocalFunctionLibrary(string? libraryName) =>
        string.Equals(libraryName, "<Use local function group>", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeLibraryName(string? libraryName)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return null;
        }

        return libraryName.Trim();
    }

    private string? ResolveProgramPath(string functionName)
    {
        var root = TestProgramDiscovery.FindRoot(_context.ProgramDirectory) ?? _context.ProgramDirectory;
        var programs = TestProgramDiscovery.EnumeratePrograms(root);
        var match = programs.FirstOrDefault(info =>
            string.Equals(Path.GetFileNameWithoutExtension(info.FileName), functionName, StringComparison.OrdinalIgnoreCase));

        return match?.FilePath;
    }

    private sealed record CallAssignment(string Destination, string Expression);
    private sealed record CallAssignmentValue(string Destination, object? Value);
}
