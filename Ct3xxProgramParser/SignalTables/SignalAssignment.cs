// Provides Signal Assignment for the program parser signal table support.
using System;

namespace Ct3xxProgramParser.SignalTables;

/// <summary>
/// Represents the signal assignment.
/// </summary>
public sealed class SignalAssignment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalAssignment"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the channel.
    /// </summary>
    public int Channel { get; }
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the board token.
    /// </summary>
    public string BoardToken { get; }
    /// <summary>
    /// Gets the board number.
    /// </summary>
    public int? BoardNumber { get; }
    /// <summary>
    /// Gets the comment.
    /// </summary>
    public string? Comment { get; }
    /// <summary>
    /// Gets the module name.
    /// </summary>
    public string? ModuleName { get; }

    /// <summary>
    /// Gets a value indicating whether the onical name condition is met.
    /// </summary>
    public string CanonicalName => string.IsNullOrWhiteSpace(ModuleName)
        ? $"{Channel}_{BoardToken}".TrimEnd('_')
        : $"{ModuleName}_{Channel}_{BoardToken}".TrimEnd('_');
}
