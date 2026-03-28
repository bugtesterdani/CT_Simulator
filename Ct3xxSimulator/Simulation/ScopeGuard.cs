// Provides Scope Guard for the simulator core simulation support.
using System;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes a configured cleanup action once when disposed.
/// </summary>
internal sealed class ScopeGuard : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeGuard"/> class.
    /// </summary>
    public ScopeGuard(Action onDispose)
    {
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    /// <summary>
    /// Executes dispose.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onDispose();
    }
}
