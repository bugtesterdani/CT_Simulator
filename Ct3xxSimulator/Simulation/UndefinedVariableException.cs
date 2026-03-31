using System;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Raised when a variable is accessed but not defined in any scope.
/// </summary>
public sealed class UndefinedVariableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UndefinedVariableException"/> class.
    /// </summary>
    /// <param name="variableName">The missing variable name.</param>
    public UndefinedVariableException(string variableName)
        : base($"Variable ist nicht definiert: {variableName}")
    {
        VariableName = variableName;
    }

    /// <summary>
    /// Gets the missing variable name.
    /// </summary>
    public string VariableName { get; }
}
