namespace JevDotNet.Models;

/// <summary>
/// Defines a Noul question built at runtime with a caller-chosen ID.
/// </summary>
public sealed record JevNoulQuestion
{
    /// <summary>
    /// Initializes a Noul question.
    /// </summary>
    /// <param name="id">The question ID. Answers are returned under this ID.</param>
    /// <param name="instructions">The question Jev should answer.</param>
    /// <param name="trueDescription">The optional description of the true outcome. Requires <paramref name="falseDescription"/>.</param>
    /// <param name="falseDescription">The optional description of the false outcome. Requires <paramref name="trueDescription"/>.</param>
    public JevNoulQuestion(string id, string instructions, string? trueDescription = null, string? falseDescription = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                $"A Noul question ID cannot be empty. Choose an ID such as 'p0' so its answer can be read back. " +
                $"See {JevDotNet.WikiLinks.Troubleshooting}.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new ArgumentException(
                $"Noul question '{id}' has empty instructions. Provide one specific question. " +
                $"See {JevDotNet.WikiLinks.Noul}.",
                nameof(instructions));
        }

        if ((trueDescription is null) != (falseDescription is null))
        {
            throw new ArgumentException(
                $"Noul question '{id}' defines only one outcome description. " +
                $"Set both True and False, or remove both. See {JevDotNet.WikiLinks.Noul}.",
                nameof(trueDescription));
        }

        Id = id;
        Instructions = instructions;
        True = trueDescription;
        False = falseDescription;
    }

    /// <summary>
    /// Gets the question ID. Answers are returned under this ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the question Jev should answer.
    /// </summary>
    public string Instructions { get; }

    /// <summary>
    /// Gets the optional description of the true outcome.
    /// </summary>
    public string? True { get; }

    /// <summary>
    /// Gets the optional description of the false outcome.
    /// </summary>
    public string? False { get; }
}
