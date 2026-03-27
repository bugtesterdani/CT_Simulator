namespace Ct3xxSimulator.TestTypes.Abstractions;

/// <summary>
/// Describes one registered test type handler for discovery and diagnostics.
/// </summary>
/// <param name="TestId">The primary CT3xx test identifier.</param>
/// <param name="Category">The logical handler category.</param>
/// <param name="Description">The human-readable handler description.</param>
public sealed record TestTypeHandlerDescriptor(string TestId, string Category, string Description);
