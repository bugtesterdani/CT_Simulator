// Provides Simulation Node View Model for the desktop application view model support.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Defines the node status values.
/// </summary>
public enum NodeStatus
{
    Pending,
    Running,
    Completed,
    Skipped,
    Failed
}

/// <summary>
/// Defines the node type values.
/// </summary>
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

/// <summary>
/// Represents the simulation node view model.
/// </summary>
public class SimulationNodeViewModel : INotifyPropertyChanged
{
    private NodeStatus _status = NodeStatus.Pending;
    private string? _lastResult;
    private object? _details;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationNodeViewModel"/> class.
    /// </summary>
    public SimulationNodeViewModel(string title, NodeType type, object source)
    {
        Title = title;
        NodeType = type;
        Source = source;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the node type.
    /// </summary>
    public NodeType NodeType { get; }
    /// <summary>
    /// Gets the source.
    /// </summary>
    public object Source { get; }
    /// <summary>
    /// Executes new.
    /// </summary>
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

    /// <summary>
    /// Executes reset.
    /// </summary>
    public void Reset()
    {
        Status = NodeStatus.Pending;
        LastResult = null;
        foreach (var child in Children)
        {
            child.Reset();
        }
    }

    /// <summary>
    /// Occurs when PropertyChanged is raised.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Executes OnPropertyChanged.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
