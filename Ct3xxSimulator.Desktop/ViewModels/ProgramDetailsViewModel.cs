// Provides Program Details View Model for the desktop application view model support.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the program details view model.
/// </summary>
public class ProgramDetailsViewModel
{
    /// <summary>
    /// Gets the program id.
    /// </summary>
    public string? ProgramId { get; init; }
    /// <summary>
    /// Gets the revision.
    /// </summary>
    public string? Revision { get; init; }
    /// <summary>
    /// Gets the author.
    /// </summary>
    public string? Author { get; init; }
    /// <summary>
    /// Gets the version.
    /// </summary>
    public string? Version { get; init; }
    /// <summary>
    /// Gets the comment.
    /// </summary>
    public string? Comment { get; init; }
    /// <summary>
    /// Gets the dut name.
    /// </summary>
    public string? DutName { get; init; }
    /// <summary>
    /// Gets the dut revision.
    /// </summary>
    public string? DutRevision { get; init; }
    /// <summary>
    /// Gets the dut variant.
    /// </summary>
    public string? DutVariant { get; init; }
    /// <summary>
    /// Gets the fixture code.
    /// </summary>
    public string? FixtureCode { get; init; }
    /// <summary>
    /// Gets the handling code.
    /// </summary>
    public string? HandlingCode { get; init; }
    /// <summary>
    /// Gets the lot code.
    /// </summary>
    public string? LotCode { get; init; }

    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<OperatorDisplayViewModel> OperatorDisplays { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<UserButtonViewModel> UserButtons { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the operator displays condition is met.
    /// </summary>
    public bool HasOperatorDisplays => OperatorDisplays.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the user buttons condition is met.
    /// </summary>
    public bool HasUserButtons => UserButtons.Count > 0;
}

/// <summary>
/// Represents the operator display view model.
/// </summary>
public class OperatorDisplayViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperatorDisplayViewModel"/> class.
    /// </summary>
    public OperatorDisplayViewModel(string title, string text)
    {
        Title = title;
        Text = text;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the text.
    /// </summary>
    public string Text { get; }
}

/// <summary>
/// Represents the user button view model.
/// </summary>
public class UserButtonViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserButtonViewModel"/> class.
    /// </summary>
    public UserButtonViewModel(string label, string? library, string? function, bool isEnabled)
    {
        Label = label;
        Library = library;
        Function = function;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Gets the label.
    /// </summary>
    public string Label { get; }
    /// <summary>
    /// Gets the library.
    /// </summary>
    public string? Library { get; }
    /// <summary>
    /// Gets the function.
    /// </summary>
    public string? Function { get; }
    /// <summary>
    /// Gets a value indicating whether the enabled condition is met.
    /// </summary>
    public bool IsEnabled { get; }
    /// <summary>
    /// Gets a value indicating whether the library condition is met.
    /// </summary>
    public bool HasLibrary => !string.IsNullOrWhiteSpace(Library);
    /// <summary>
    /// Gets a value indicating whether the function condition is met.
    /// </summary>
    public bool HasFunction => !string.IsNullOrWhiteSpace(Function);
    /// <summary>
    /// Gets the status text.
    /// </summary>
    public string StatusText => IsEnabled ? "Aktiv" : "Deaktiviert";
}

/// <summary>
/// Represents the table details view model.
/// </summary>
public class TableDetailsViewModel
{
    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; init; } = "Tabelle";
    /// <summary>
    /// Gets the file.
    /// </summary>
    public string? File { get; init; }
    /// <summary>
    /// Gets the digest.
    /// </summary>
    public string? Digest { get; init; }
    /// <summary>
    /// Gets the interface file.
    /// </summary>
    public string? InterfaceFile { get; init; }
    /// <summary>
    /// Gets the interface digest.
    /// </summary>
    public string? InterfaceDigest { get; init; }
    /// <summary>
    /// Gets the length.
    /// </summary>
    public string? Length { get; init; }

    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<TableVariableViewModel> Variables { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<TableRecordViewModel> Records { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<TableFileEntryViewModel> Files { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<LibrarySummaryViewModel> Libraries { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the file condition is met.
    /// </summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(File);
    /// <summary>
    /// Gets a value indicating whether the interface condition is met.
    /// </summary>
    public bool HasInterface => !string.IsNullOrWhiteSpace(InterfaceFile);
    /// <summary>
    /// Gets a value indicating whether the variables condition is met.
    /// </summary>
    public bool HasVariables => Variables.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the records condition is met.
    /// </summary>
    public bool HasRecords => Records.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the files condition is met.
    /// </summary>
    public bool HasFiles => Files.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the libraries condition is met.
    /// </summary>
    public bool HasLibraries => Libraries.Count > 0;
}

/// <summary>
/// Represents the table variable view model.
/// </summary>
public class TableVariableViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableVariableViewModel"/> class.
    /// </summary>
    public TableVariableViewModel(string name, string? type, string? initial)
    {
        Name = name;
        Type = type;
        Initial = initial;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the type.
    /// </summary>
    public string? Type { get; }
    /// <summary>
    /// Gets the initial.
    /// </summary>
    public string? Initial { get; }
}

/// <summary>
/// Represents the table record view model.
/// </summary>
public class TableRecordViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableRecordViewModel"/> class.
    /// </summary>
    public TableRecordViewModel(string title, string? destination, string? expression)
    {
        Title = title;
        Destination = destination;
        Expression = expression;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the destination.
    /// </summary>
    public string? Destination { get; }
    /// <summary>
    /// Gets the expression.
    /// </summary>
    public string? Expression { get; }
}

/// <summary>
/// Represents the table file entry view model.
/// </summary>
public class TableFileEntryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableFileEntryViewModel"/> class.
    /// </summary>
    public TableFileEntryViewModel(string name, string? digest)
    {
        Name = name;
        Digest = digest;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the digest.
    /// </summary>
    public string? Digest { get; }
}

/// <summary>
/// Represents the library summary view model.
/// </summary>
public class LibrarySummaryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySummaryViewModel"/> class.
    /// </summary>
    public LibrarySummaryViewModel(string title, int functionCount)
    {
        Title = title;
        FunctionCount = functionCount;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the function count.
    /// </summary>
    public int FunctionCount { get; }
    /// <summary>
    /// Gets the subtitle.
    /// </summary>
    public string Subtitle => FunctionCount == 1
        ? "1 Funktion"
        : $"{FunctionCount} Funktionen";
}

/// <summary>
/// Represents the library details view model.
/// </summary>
public class LibraryDetailsViewModel
{
    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; init; } = "Bibliothek";
    /// <summary>
    /// Gets the revision.
    /// </summary>
    public string? Revision { get; init; }
    /// <summary>
    /// Gets a value indicating whether the external condition is met.
    /// </summary>
    public bool IsExternal { get; init; }
    /// <summary>
    /// Gets a value indicating whether the h condition is met.
    /// </summary>
    public string? Hash { get; init; }
    /// <summary>
    /// Gets the function count.
    /// </summary>
    public int FunctionCount { get; init; }
    /// <summary>
    /// Gets a value indicating whether the hash condition is met.
    /// </summary>
    public bool HasHash => !string.IsNullOrWhiteSpace(Hash);
    /// <summary>
    /// Gets the external display.
    /// </summary>
    public string ExternalDisplay => IsExternal ? "Ja" : "Nein";
}

/// <summary>
/// Represents the library function details view model.
/// </summary>
public class LibraryFunctionDetailsViewModel
{
    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; init; } = "Funktion";
    /// <summary>
    /// Gets the exec condition.
    /// </summary>
    public string? ExecCondition { get; init; }
    /// <summary>
    /// Gets the log prefix.
    /// </summary>
    public string? LogPrefix { get; init; }
    /// <summary>
    /// Gets the log suffix.
    /// </summary>
    public string? LogSuffix { get; init; }
    /// <summary>
    /// Gets a value indicating whether the h condition is met.
    /// </summary>
    public string? Hash { get; init; }
    /// <summary>
    /// Gets a value indicating whether the disabled condition is met.
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the exec condition condition is met.
    /// </summary>
    public bool HasExecCondition => !string.IsNullOrWhiteSpace(ExecCondition);
    /// <summary>
    /// Gets a value indicating whether the logging condition is met.
    /// </summary>
    public bool HasLogging => !string.IsNullOrWhiteSpace(LogPrefix) || !string.IsNullOrWhiteSpace(LogSuffix);
    /// <summary>
    /// Gets a value indicating whether the hash condition is met.
    /// </summary>
    public bool HasHash => !string.IsNullOrWhiteSpace(Hash);
    /// <summary>
    /// Gets the status text.
    /// </summary>
    public string StatusText => IsDisabled ? "Deaktiviert" : "Aktiv";
}

public static class ProgramDetailsFactory
{
    /// <summary>
    /// Creates the program details.
    /// </summary>
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

    /// <summary>
    /// Creates the table details.
    /// </summary>
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

    /// <summary>
    /// Creates the library details.
    /// </summary>
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

    /// <summary>
    /// Creates the library function details.
    /// </summary>
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

    /// <summary>
    /// Executes EnumerateDisplays.
    /// </summary>
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

    /// <summary>
    /// Executes EnumerateButtons.
    /// </summary>
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

    /// <summary>
    /// Executes Clean.
    /// </summary>
    private static string? Clean(string? value) => TestDetailsFactory.Clean(value);
}
