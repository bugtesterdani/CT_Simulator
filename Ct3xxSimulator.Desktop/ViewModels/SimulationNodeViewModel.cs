using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ct3xxSimulator.Desktop.ViewModels;

public enum NodeStatus
{
    Pending,
    Running,
    Completed,
    Skipped,
    Failed
}

public enum NodeType
{
    Program,
    Section,
    Group,
    DutLoop,
    Test,
    Table,
    Library,
    Function
}

public class SimulationNodeViewModel : INotifyPropertyChanged
{
    private NodeStatus _status = NodeStatus.Pending;
    private string? _lastResult;
    private object? _details;

    public SimulationNodeViewModel(string title, NodeType type, object source)
    {
        Title = title;
        NodeType = type;
        Source = source;
    }

    public string Title { get; }
    public NodeType NodeType { get; }
    public object Source { get; }
    public ObservableCollection<SimulationNodeViewModel> Children { get; } = new();
    public object? Details
    {
        get => _details;
        set
        {
            if (!ReferenceEquals(_details, value))
            {
                _details = value;
                OnPropertyChanged();
            }
        }
    }

    public NodeStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public string? LastResult
    {
        get => _lastResult;
        set
        {
            if (_lastResult != value)
            {
                _lastResult = value;
                OnPropertyChanged();
            }
        }
    }

    public void Reset()
    {
        Status = NodeStatus.Pending;
        LastResult = null;
        foreach (var child in Children)
        {
            child.Reset();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
