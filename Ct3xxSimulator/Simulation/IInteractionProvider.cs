using System.Collections.Generic;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Defines interactive prompts used by simulator test types that require operator input.
/// </summary>
public interface IInteractionProvider
{
    /// <summary>
    /// Prompts the user to select one option from a predefined list.
    /// </summary>
    string PromptSelection(string message, IReadOnlyList<string> options);
    /// <summary>
    /// Prompts the user for free-form text input.
    /// </summary>
    string PromptInput(string prompt);
    /// <summary>
    /// Prompts the user for a pass/fail decision.
    /// </summary>
    bool PromptPassFail(string message);
    /// <summary>
    /// Shows an informational message to the user.
    /// </summary>
    void ShowMessage(string message, bool requiresConfirmation);
}

/// <summary>
/// Extends <see cref="IInteractionProvider"/> with measurement-specific prompting.
/// </summary>
public interface IMeasurementInteractionProvider : IInteractionProvider
{
    /// <summary>
    /// Prompts the user for a measurement value.
    /// </summary>
    string PromptMeasurement(Test test, Record record, string prompt, string? unit);
}

/// <summary>
/// Implements interaction prompts by using standard console input and output.
/// </summary>
public sealed class ConsoleInteractionProvider : IMeasurementInteractionProvider
{
    /// <inheritdoc />
    public string PromptSelection(string message, IReadOnlyList<string> options)
    {
        if (options.Count == 0)
        {
            return PromptInput(message);
        }

        while (true)
        {
            Console.WriteLine(message);
            for (var i = 0; i < options.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {options[i]}");
            }

            Console.Write("Select option: ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var index) && index >= 1 && index <= options.Count)
            {
                return options[index - 1];
            }

            Console.WriteLine("Invalid selection, please try again.");
        }
    }

    /// <inheritdoc />
    public string PromptInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    /// <inheritdoc />
    public bool PromptPassFail(string message)
    {
        while (true)
        {
            Console.Write($"{message} (p=PASS / f=FAIL): ");
            var input = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            if (input is "p" or "pass")
            {
                return true;
            }

            if (input is "f" or "fail")
            {
                return false;
            }

            Console.WriteLine("Please enter 'p' or 'f'.");
        }
    }

    /// <inheritdoc />
    public void ShowMessage(string message, bool requiresConfirmation)
    {
        Console.WriteLine(message);
        if (requiresConfirmation)
        {
            Console.WriteLine("Press ENTER to continue...");
            Console.ReadLine();
        }
    }

    /// <inheritdoc />
    public string PromptMeasurement(Test test, Record record, string prompt, string? unit)
    {
        var display = string.IsNullOrWhiteSpace(unit) ? prompt : $"{prompt.TrimEnd()} [{unit}] ";
        return PromptInput(display);
    }
}
