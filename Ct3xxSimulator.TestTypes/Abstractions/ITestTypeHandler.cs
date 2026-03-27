using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.TestTypes.Abstractions;

/// <summary>
/// Defines the contract for one pluggable simulator handler that can execute a CT3xx test type.
/// </summary>
public interface ITestTypeHandler
{
    /// <summary>
    /// Gets the primary CT3xx test identifier handled by this implementation.
    /// </summary>
    string TestId { get; }
    /// <summary>
    /// Determines whether the handler can process the supplied test instance.
    /// </summary>
    /// <param name="test">The CT3xx test to inspect.</param>
    /// <returns><see langword="true"/> when the handler can process the test.</returns>
    bool CanHandle(Test test);
}
