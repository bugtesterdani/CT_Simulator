// Provides card inventory validation data contracts.
using System.Collections.Generic;

namespace Ct3xxSimulator.Validation;

/// <summary>
/// Defines the tester card inventory and mapping configuration.
/// </summary>
public sealed class CardInventoryDefinition
{
    /// <summary>
    /// Gets or sets the installed tester cards by card name.
    /// </summary>
    public Dictionary<string, int> InstalledCards { get; set; } = new();
    /// <summary>
    /// Gets or sets the test type to card mapping.
    /// Use "PC" for test types that do not require a tester card.
    /// </summary>
    public Dictionary<string, string> TestTypeCards { get; set; } = new();
    /// <summary>
    /// Gets or sets the optional test type card rules.
    /// </summary>
    public List<TestTypeCardRule> TestTypeCardRules { get; set; } = new();
    /// <summary>
    /// Gets or sets the optional card index regex per card name.
    /// </summary>
    public Dictionary<string, string> CardIndexPatterns { get; set; } = new();
}

/// <summary>
/// Represents one mapping rule between a test type and required cards.
/// </summary>
public sealed class TestTypeCardRule
{
    /// <summary>
    /// Gets or sets the test type (e.g., ICT, CTCT, SHRT, 2ARB).
    /// </summary>
    public string TestType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the required cards for this test type.
    /// Use "PC" to indicate no tester card is required.
    /// </summary>
    public List<string> Cards { get; set; } = new();
    /// <summary>
    /// Gets or sets the optional regex that must match the test content for this rule to apply.
    /// </summary>
    public string? MatchRegex { get; set; }
}
