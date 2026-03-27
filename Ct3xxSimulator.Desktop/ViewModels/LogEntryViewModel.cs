// Provides Log Entry View Model for the desktop application view model support.
using System;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the log entry view model.
/// </summary>
public class LogEntryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogEntryViewModel"/> class.
    /// </summary>
    public LogEntryViewModel(string message)
        : this(message, DateTime.Now)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEntryViewModel"/> class.
    /// </summary>
    public LogEntryViewModel(string message, DateTime timestamp)
    {
        Timestamp = timestamp;
        Message = message;
    }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public string Message { get; }
}
