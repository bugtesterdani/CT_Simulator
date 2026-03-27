using System.Threading;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Controls debugger-like pauses around test, group and snapshot boundaries.
/// </summary>
public interface ISimulationExecutionController
{
    /// <summary>
    /// Waits immediately before a test is executed.
    /// </summary>
    /// <param name="test">The test about to run.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    void WaitBeforeTest(Test test, CancellationToken cancellationToken);

    /// <summary>
    /// Waits immediately after a test completed.
    /// </summary>
    /// <param name="test">The completed test.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    void WaitAfterTest(Test test, CancellationToken cancellationToken);

    /// <summary>
    /// Waits immediately after a group completed.
    /// </summary>
    /// <param name="group">The completed group.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    void WaitAfterGroup(Group group, CancellationToken cancellationToken);

    /// <summary>
    /// Waits after a snapshot was published to the UI or export layers.
    /// </summary>
    /// <param name="snapshot">The published snapshot.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    void WaitAfterSnapshot(SimulationStateSnapshot snapshot, CancellationToken cancellationToken);
}

/// <summary>
/// Provides a no-op execution controller.
/// </summary>
public sealed class NullSimulationExecutionController : ISimulationExecutionController
{
    /// <inheritdoc />
    public void WaitBeforeTest(Test test, CancellationToken cancellationToken)
    {
    }

    /// <inheritdoc />
    public void WaitAfterTest(Test test, CancellationToken cancellationToken)
    {
    }

    /// <inheritdoc />
    public void WaitAfterGroup(Group group, CancellationToken cancellationToken)
    {
    }

    /// <inheritdoc />
    public void WaitAfterSnapshot(SimulationStateSnapshot snapshot, CancellationToken cancellationToken)
    {
    }
}
