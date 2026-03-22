using System.Collections.Generic;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

public interface IInteractionProvider
{
    string PromptSelection(string message, IReadOnlyList<string> options);
    string PromptInput(string prompt);
    bool PromptPassFail(string message);
    void ShowMessage(string message, bool requiresConfirmation);
}

public interface IMeasurementInteractionProvider : IInteractionProvider
{
    string PromptMeasurement(Test test, Record record, string prompt, string? unit);
}

public sealed class ConsoleInteractionProvider : IMeasurementInteractionProvider
{
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

    public string PromptInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

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

    public void ShowMessage(string message, bool requiresConfirmation)
    {
        Console.WriteLine(message);
        if (requiresConfirmation)
        {
            Console.WriteLine("Press ENTER to continue...");
            Console.ReadLine();
        }
    }

    public string PromptMeasurement(Test test, Record record, string prompt, string? unit)
    {
        var display = string.IsNullOrWhiteSpace(unit) ? prompt : $"{prompt.TrimEnd()} [{unit}] ";
        return PromptInput(display);
    }
}
