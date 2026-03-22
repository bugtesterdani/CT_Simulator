using System;

namespace Ct3xxProgramParser.SignalTables;

public sealed class SignalAssignment
{
    public SignalAssignment(int channel, string name, string boardToken, string? comment, string? moduleName = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Signal name must not be empty.", nameof(name));
        }

        Channel = channel;
        Name = name.Trim();
        BoardToken = boardToken ?? string.Empty;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment;
        ModuleName = string.IsNullOrWhiteSpace(moduleName) ? null : moduleName!.Trim();
        BoardNumber = int.TryParse(boardToken, out var number)
            ? number
            : null;
    }

    public int Channel { get; }
    public string Name { get; }
    public string BoardToken { get; }
    public int? BoardNumber { get; }
    public string? Comment { get; }
    public string? ModuleName { get; }

    public string CanonicalName => string.IsNullOrWhiteSpace(ModuleName)
        ? $"{Channel}_{BoardToken}".TrimEnd('_')
        : $"{ModuleName}_{Channel}_{BoardToken}".TrimEnd('_');
}
