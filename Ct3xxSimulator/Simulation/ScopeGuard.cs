using System;

namespace Ct3xxSimulator.Simulation;

internal sealed class ScopeGuard : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    public ScopeGuard(Action onDispose)
    {
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

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
