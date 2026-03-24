using System.Threading;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

public interface ISimulationExecutionController
{
    void WaitBeforeTest(Test test, CancellationToken cancellationToken);
    void WaitAfterTest(Test test, CancellationToken cancellationToken);
}

public sealed class NullSimulationExecutionController : ISimulationExecutionController
{
    public void WaitBeforeTest(Test test, CancellationToken cancellationToken)
    {
    }

    public void WaitAfterTest(Test test, CancellationToken cancellationToken)
    {
    }
}
