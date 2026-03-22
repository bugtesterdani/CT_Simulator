using System;

namespace Ct3xxSimulator.Desktop.ViewModels;

public class LogEntryViewModel
{
    public LogEntryViewModel(string message)
    {
        Timestamp = DateTime.Now;
        Message = message;
    }

    public DateTime Timestamp { get; }
    public string Message { get; }
}
