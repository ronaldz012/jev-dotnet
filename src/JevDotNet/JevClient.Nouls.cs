using System.Text.Json;
using JevDotNet.Models;

namespace JevDotNet;

public partial class JevClient
{
    /// <summary>
    /// Evaluates a state against runtime-built Noul questions and returns one probability per question ID.
    /// </summary>
    /// <typeparam name="TState">The state type. A plain string for text, or structured data such as an object or array.</typeparam>
    /// <param name="state">The state to evaluate.</param>
    /// <param name="questions">The Noul questions to ask. Each ID must be unique.</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>The probabilities keyed by question ID, in question order, together with the token usage.</returns>
    public async Task<JevResponse<IReadOnlyDictionary<string, JevNoul>>> EvaluateNoulsAsync<TState>(
        TState state,
        IEnumerable<JevNoulQuestion> questions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(questions);

        List<JevNoulQuestion> questionList = questions.ToList();
        if (questionList.Count == 0)
        {
            throw new InvalidOperationException(
                $"No Noul questions were provided. Add at least one JevNoulQuestion. " +
                $"See {WikiLinks.Noul}.");
        }

        Dictionary<string, object> payload = new(StringComparer.OrdinalIgnoreCase);
        foreach (JevNoulQuestion question in questionList)
        {
            if (!payload.TryAdd(question.Id, BuildNoulQuestion(question)))
            {
                throw new InvalidOperationException(
                    $"Multiple questions use the ID '{question.Id}' (IDs are case-insensitive). " +
                    $"Give each JevNoulQuestion a unique ID. See {WikiLinks.Troubleshooting}.");
            }
        }

        JevResponse rawResponse = await SendAsync(state, payload, cancellationToken);

        Dictionary<string, JevNoul> answers = new(questionList.Count);
        foreach (JevNoulQuestion question in questionList)
        {
            answers.Add(question.Id, new JevNoul(ReadNoulProbability(question.Id, rawResponse)));
        }

        return new JevResponse<IReadOnlyDictionary<string, JevNoul>>(
            answers,
            rawResponse.Usage.InputTokenCount,
            rawResponse.Usage.OutputTokenCount);
    }

    private static object BuildNoulQuestion(JevNoulQuestion question) =>
        question.True is null
            ? new { type = "noul", instructions = question.Instructions }
            : new
            {
                type = "noul",
                instructions = question.Instructions,
                criteria = new Dictionary<string, string>
                {
                    ["true"] = question.True,
                    ["false"] = question.False!
                }
            };

    private static double ReadNoulProbability(string id, JevResponse response)
    {
        if (!response.Answers.TryGetValue(id, out JsonElement answer))
        {
            throw new JsonException(
                $"The API response has no answer for question ID '{id}'. " +
                $"Received answer IDs: {string.Join(", ", response.Answers.Keys)}. " +
                $"See {WikiLinks.Troubleshooting}.");
        }

        if (answer.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                $"Answer '{id}' must be a JSON object for Noul. Check the API response shape. " +
                $"See {WikiLinks.Noul}.");
        }

        if (!answer.TryGetProperty("type", out JsonElement type) ||
            type.ValueKind != JsonValueKind.String ||
            type.GetString() != "noul")
        {
            throw new JsonException(
                $"Answer '{id}' does not have type 'noul'. Check the question ID and API response. " +
                $"See {WikiLinks.Noul}.");
        }

        if (!answer.TryGetProperty("noul", out JsonElement noul))
        {
            throw new JsonException(
                $"Answer '{id}' is missing required 'noul' field. Check the API response shape. " +
                $"See {WikiLinks.Noul}.");
        }

        if (noul.ValueKind != JsonValueKind.Number || !noul.TryGetDouble(out double probability))
        {
            throw new JsonException(
                $"Answer '{id}' field 'noul' must be a number. See {WikiLinks.Noul}.");
        }

        if (probability is < 0 or > 1)
        {
            throw new JsonException(
                $"Answer '{id}' field 'noul' must be between 0 and 1, but was {probability}. " +
                $"See {WikiLinks.Noul}.");
        }

        return probability;
    }
}
