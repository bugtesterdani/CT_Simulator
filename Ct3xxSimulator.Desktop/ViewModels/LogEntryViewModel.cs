using System;

namespace Ct3xxSimulator.Desktop.ViewModels;

public class LogEntryViewModel
{
    public LogEntryViewModel(string message)
        : this(message, DateTime.Now)
    {
    }

    public LogEntryViewModel(string message, DateTime timestamp)
    {
        Timestamp = timestamp;
        Message = message;
    }

    public DateTime Timestamp { get; }
    public string Message { get; }
}
