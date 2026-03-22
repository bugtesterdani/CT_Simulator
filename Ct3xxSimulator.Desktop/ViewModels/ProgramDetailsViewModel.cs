using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Desktop.ViewModels;

public class ProgramDetailsViewModel
{
    public string? ProgramId { get; init; }
    public string? Revision { get; init; }
    public string? Author { get; init; }
    public string? Version { get; init; }
    public string? Comment { get; init; }
    public string? DutName { get; init; }
    public string? DutRevision { get; init; }
    public string? DutVariant { get; init; }
    public string? FixtureCode { get; init; }
    public string? HandlingCode { get; init; }
    public string? LotCode { get; init; }

    public ObservableCollection<OperatorDisplayViewModel> OperatorDisplays { get; } = new();
    public ObservableCollection<UserButtonViewModel> UserButtons { get; } = new();

    public bool HasOperatorDisplays => OperatorDisplays.Count > 0;
    public bool HasUserButtons => UserButtons.Count > 0;
}

public class OperatorDisplayViewModel
{
    public OperatorDisplayViewModel(string title, string text)
    {
        Title = title;
        Text = text;
    }

    public string Title { get; }
    public string Text { get; }
}

public class UserButtonViewModel
{
    public UserButtonViewModel(string label, string? library, string? function, bool isEnabled)
    {
        Label = label;
        Library = library;
        Function = function;
        IsEnabled = isEnabled;
    }

    public string Label { get; }
    public string? Library { get; }
    public string? Function { get; }
    public bool IsEnabled { get; }
    public bool HasLibrary => !string.IsNullOrWhiteSpace(Library);
    public bool HasFunction => !string.IsNullOrWhiteSpace(Function);
    public string StatusText => IsEnabled ? "Aktiv" : "Deaktiviert";
}

public class TableDetailsViewModel
{
    public string Title { get; init; } = "Tabelle";
    public string? File { get; init; }
    public string? Digest { get; init; }
    public string? InterfaceFile { get; init; }
    public string? InterfaceDigest { get; init; }
    public string? Length { get; init; }

    public ObservableCollection<TableVariableViewModel> Variables { get; } = new();
    public ObservableCollection<TableRecordViewModel> Records { get; } = new();
    public ObservableCollection<TableFileEntryViewModel> Files { get; } = new();
    public ObservableCollection<LibrarySummaryViewModel> Libraries { get; } = new();

    public bool HasFile => !string.IsNullOrWhiteSpace(File);
    public bool HasInterface => !string.IsNullOrWhiteSpace(InterfaceFile);
    public bool HasVariables => Variables.Count > 0;
    public bool HasRecords => Records.Count > 0;
    public bool HasFiles => Files.Count > 0;
    public bool HasLibraries => Libraries.Count > 0;
}

public class TableVariableViewModel
{
    public TableVariableViewModel(string name, string? type, string? initial)
    {
        Name = name;
        Type = type;
        Initial = initial;
    }

    public string Name { get; }
    public string? Type { get; }
    public string? Initial { get; }
}

public class TableRecordViewModel
{
    public TableRecordViewModel(string title, string? destination, string? expression)
    {
        Title = title;
        Destination = destination;
        Expression = expression;
    }

    public string Title { get; }
    public string? Destination { get; }
    public string? Expression { get; }
}

public class TableFileEntryViewModel
{
    public TableFileEntryViewModel(string name, string? digest)
    {
        Name = name;
        Digest = digest;
    }

    public string Name { get; }
    public string? Digest { get; }
}

public class LibrarySummaryViewModel
{
    public LibrarySummaryViewModel(string title, int functionCount)
    {
        Title = title;
        FunctionCount = functionCount;
    }

    public string Title { get; }
    public int FunctionCount { get; }
    public string Subtitle => FunctionCount == 1
        ? "1 Funktion"
        : $"{FunctionCount} Funktionen";
}

public class LibraryDetailsViewModel
{
    public string Title { get; init; } = "Bibliothek";
    public string? Revision { get; init; }
    public bool IsExternal { get; init; }
    public string? Hash { get; init; }
    public int FunctionCount { get; init; }
    public bool HasHash => !string.IsNullOrWhiteSpace(Hash);
    public string ExternalDisplay => IsExternal ? "Ja" : "Nein";
}

public class LibraryFunctionDetailsViewModel
{
    public string Title { get; init; } = "Funktion";
    public string? ExecCondition { get; init; }
    public string? LogPrefix { get; init; }
    public string? LogSuffix { get; init; }
    public string? Hash { get; init; }
    public bool IsDisabled { get; init; }

    public bool HasExecCondition => !string.IsNullOrWhiteSpace(ExecCondition);
    public bool HasLogging => !string.IsNullOrWhiteSpace(LogPrefix) || !string.IsNullOrWhiteSpace(LogSuffix);
    public bool HasHash => !string.IsNullOrWhiteSpace(Hash);
    public string StatusText => IsDisabled ? "Deaktiviert" : "Aktiv";
}

public static class ProgramDetailsFactory
{
    public static ProgramDetailsViewModel CreateProgramDetails(Ct3xxProgram program)
    {
        var viewModel = new ProgramDetailsViewModel
        {
            ProgramId = Clean(program.Id),
            Revision = Clean(program.Revision),
            Author = Clean(program.ProgramAuthor),
            Version = Clean(program.ProgramVersion),
            Comment = Clean(program.ProgramComment),
            DutName = Clean(program.DutName),
            DutRevision = Clean(program.DutRevision),
            DutVariant = Clean(program.DutVariant),
            FixtureCode = Clean(program.FixtureCode),
            HandlingCode = Clean(program.HandlingCode),
            LotCode = Clean(program.LotCode)
        };

        foreach (var display in EnumerateDisplays(program.OperatorScreen))
        {
            viewModel.OperatorDisplays.Add(display);
        }

        foreach (var button in EnumerateButtons(program.UserButtons))
        {
            viewModel.UserButtons.Add(button);
        }

        return viewModel;
    }

    public static TableDetailsViewModel CreateTableDetails(Table table)
    {
        var viewModel = new TableDetailsViewModel
        {
            Title = Clean(table.Id) ?? Clean(table.File) ?? "Tabelle",
            File = Clean(table.File),
            Digest = Clean(table.Digest),
            InterfaceFile = Clean(table.InterfaceFile),
            InterfaceDigest = Clean(table.InterfaceDigest),
            Length = Clean(table.Length)
        };

        foreach (var variable in table.Variables)
        {
            var name = Clean(variable.Name) ?? variable.Id ?? "Variable";
            viewModel.Variables.Add(new TableVariableViewModel(
                name,
                Clean(variable.Type),
                Clean(variable.Initial)));
        }

        foreach (var record in table.Records)
        {
            var title = Clean(record.Text) ?? Clean(record.DrawingReference) ?? record.Id ?? "Eintrag";
            viewModel.Records.Add(new TableRecordViewModel(
                title,
                Clean(record.Destination),
                Clean(record.Expression)));
        }

        foreach (var file in table.Files)
        {
            viewModel.Files.Add(new TableFileEntryViewModel(
                Clean(file.Name) ?? file.Id ?? "Datei",
                Clean(file.Digest)));
        }

        foreach (var library in table.Libraries)
        {
            var title = Clean(library.Name) ?? library.Id ?? "Bibliothek";
            var summary = new LibrarySummaryViewModel(title, library.Functions.Count);
            viewModel.Libraries.Add(summary);
        }

        return viewModel;
    }

    public static LibraryDetailsViewModel CreateLibraryDetails(LibraryDefinition library)
    {
        return new LibraryDetailsViewModel
        {
            Title = Clean(library.Name) ?? library.Id ?? "Bibliothek",
            Revision = Clean(library.Revision),
            IsExternal = string.Equals(Clean(library.IsExternal), "yes", System.StringComparison.OrdinalIgnoreCase),
            Hash = library.Hash?.Value,
            FunctionCount = library.Functions.Count
        };
    }

    public static LibraryFunctionDetailsViewModel CreateLibraryFunctionDetails(LibraryFunction function)
    {
        return new LibraryFunctionDetailsViewModel
        {
            Title = Clean(function.Name) ?? function.Id ?? "Funktion",
            ExecCondition = Clean(function.ExecCondition),
            LogPrefix = Clean(function.LogPrefix),
            LogSuffix = Clean(function.LogSuffix),
            Hash = function.Hash?.Value,
            IsDisabled = string.Equals(Clean(function.Disabled), "yes", System.StringComparison.OrdinalIgnoreCase)
        };
    }

    private static IEnumerable<OperatorDisplayViewModel> EnumerateDisplays(OperatorScreen? screen)
    {
        if (screen == null)
        {
            yield break;
        }

        var entries = new[]
        {
            screen.Display1,
            screen.Display2,
            screen.Display3,
            screen.Display4
        };

        foreach (var entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            var title = Clean(entry.Title) ?? "Anzeige";
            var text = Clean(entry.Text) ?? "-";
            yield return new OperatorDisplayViewModel(title, text);
        }
    }

    private static IEnumerable<UserButtonViewModel> EnumerateButtons(UserButtonPanel? panel)
    {
        if (panel == null)
        {
            yield break;
        }

        var entries = new[]
        {
            panel.Button1,
            panel.Button2,
            panel.Button3,
            panel.Button4,
            panel.Button5,
            panel.Button6,
            panel.Button7
        };

        var index = 1;
        foreach (var button in entries)
        {
            if (button == null)
            {
                index++;
                continue;
            }

            var label = Clean(button.Text) ?? $"Button {index}";
            var library = Clean(button.Library);
            var function = Clean(button.Function);
            var enabled = !string.Equals(Clean(button.Enable), "no", System.StringComparison.OrdinalIgnoreCase);
            yield return new UserButtonViewModel(label, library, function, enabled);
            index++;
        }
    }

    private static string? Clean(string? value) => TestDetailsFactory.Clean(value);
}
