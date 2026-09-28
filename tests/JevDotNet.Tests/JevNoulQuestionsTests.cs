using System.Net;
using System.Text;
using System.Text.Json;
using JevDotNet.Models;

namespace JevDotNet.Tests;

public sealed class JevNoulQuestionsTests
{
    [Fact]
    public async Task EvaluateNoulsAsync_MapsDynamicAnswersByIdInQuestionOrder()
    {
        StubHttpMessageHandler handler = new StubHttpMessageHandler(ResponseJson);
        JevClient client = CreateClient(handler);

        List<JevNoulQuestion> questions = BuildServiceQuestions();
        JevResponse<IReadOnlyDictionary<string, JevNoul>> response = await client.EvaluateNoulsAsync(
            new
            {
                context = "Decide which services and components to restart after the outage.",
                services = new[]
                {
                    new { name = "Billing" },
                    new { name = "Search" }
                }
            },
            questions);

        Assert.Equal(new[] { "s0", "s0_c0", "s0_c1", "s1", "s1_c0" }, response.Result.Keys);
        Assert.Equal(0.55, response.Result["s0"].Probability);
        Assert.Equal(0.06, response.Result["s0_c1"].Probability);
        Assert.Equal(0.81, response.Result["s1_c0"].Probability);
        Assert.True(response.Result["s1_c0"].IsAtLeast(0.7));
        Assert.False(response.Result["s0"].IsAtLeast(0.7));
        Assert.Equal(120, response.InputTokenCount);
        Assert.Equal(30, response.OutputTokenCount);

        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        JsonElement root = request.RootElement;
        Assert.Equal("jev-test", root.GetProperty("model").GetString());
        Assert.Equal(
            "Decide which services and components to restart after the outage.",
            root.GetProperty("state").GetProperty("context").GetString());
        Assert.Equal(
            "Billing",
            root.GetProperty("state").GetProperty("services")[0].GetProperty("name").GetString());

        JsonElement sentQuestions = root.GetProperty("questions");
        Assert.Equal(5, sentQuestions.EnumerateObject().Count());
        Assert.Equal(
            "Should `services[0]` be restarted as a whole?",
            sentQuestions.GetProperty("s0").GetProperty("instructions").GetString());
        Assert.False(sentQuestions.GetProperty("s0").TryGetProperty("criteria", out _));
        Assert.Equal(
            "The service must be restarted",
            sentQuestions.GetProperty("s1").GetProperty("criteria").GetProperty("true").GetString());
        Assert.Equal(
            "The service must keep running",
            sentQuestions.GetProperty("s1").GetProperty("criteria").GetProperty("false").GetString());
    }

    [Fact]
    public async Task EvaluateAsync_SerializesStructuredStateForAttributeQuestions()
    {
        StubHttpMessageHandler handler = new StubHttpMessageHandler(AttributeResponseJson);
        JevClient client = CreateClient(handler);

        JevResponse<SingleNoulResult> response = await client.EvaluateAsync<SingleNoulResult>(
            new { context = "Outage policy.", errors = 150 });

        Assert.Equal(0.9, response.Result.ShouldRestart?.Probability);

        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        JsonElement state = request.RootElement.GetProperty("state");
        Assert.Equal("Outage policy.", state.GetProperty("context").GetString());
        Assert.Equal(150, state.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task EvaluateNoulsAsync_RejectsDuplicateQuestionIds()
    {
        JevClient client = CreateClient(new StubHttpMessageHandler(ResponseJson));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.EvaluateNoulsAsync(
                "state",
                [
                    new JevNoulQuestion("s0", "First?"),
                    new JevNoulQuestion("S0", "Second?")
                ]));

        Assert.Contains("ID 'S0'", exception.Message);
    }

    [Fact]
    public async Task EvaluateNoulsAsync_RejectsEmptyQuestionList()
    {
        JevClient client = CreateClient(new StubHttpMessageHandler(ResponseJson));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.EvaluateNoulsAsync<string>("state", []));

        Assert.Contains("No Noul questions", exception.Message);
    }

    [Fact]
    public async Task EvaluateNoulsAsync_ExplainsMissingAnswer()
    {
        JevClient client = CreateClient(new StubHttpMessageHandler(MissingAnswerResponseJson));

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => client.EvaluateNoulsAsync("state", BuildServiceQuestions()));

        Assert.Contains("no answer for question ID 's1_c0'", exception.Message);
        Assert.Contains("s0", exception.Message);
    }

    [Fact]
    public async Task EvaluateNoulsAsync_RejectsOutOfRangeNoul()
    {
        JevClient client = CreateClient(new StubHttpMessageHandler(OutOfRangeResponseJson));

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => client.EvaluateNoulsAsync(
                "state",
                [new JevNoulQuestion("s0", "Should it be restarted?")]));

        Assert.Contains("must be between 0 and 1", exception.Message);
    }

    [Theory]
    [InlineData(" ", "Should it be restarted?", null, "ID cannot be empty")]
    [InlineData("s0", " ", null, "empty instructions")]
    [InlineData("s0", "Should it be restarted?", "Restart it", "only one outcome description")]
    public void JevNoulQuestion_RejectsInvalidArguments(
        string id,
        string instructions,
        string? trueDescription,
        string expectedMessage)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new JevNoulQuestion(id, instructions, trueDescription: trueDescription));

        Assert.Contains(expectedMessage, exception.Message);
    }

    private static List<JevNoulQuestion> BuildServiceQuestions() =>
    [
        new JevNoulQuestion("s0", "Should `services[0]` be restarted as a whole?"),
        new JevNoulQuestion("s0_c0", "Should the component `services[0].components[0]` be restarted separately from the rest of the service?"),
        new JevNoulQuestion("s0_c1", "Should the component `services[0].components[1]` be restarted separately from the rest of the service?"),
        new JevNoulQuestion(
            "s1",
            "Should `services[1]` be restarted as a whole?",
            trueDescription: "The service must be restarted",
            falseDescription: "The service must keep running"),
        new JevNoulQuestion("s1_c0", "Should the component `services[1].components[0]` be restarted separately from the rest of the service?")
    ];

    private static JevClient CreateClient(StubHttpMessageHandler handler) => new(new JevClientOptions
    {
        ApiKey = "test-api-key",
        Model = "jev-test",
        Endpoint = new Uri("https://example.test/evaluate"),
        HttpClientFactory = () => new HttpClient(handler)
    });

    private sealed class SingleNoulResult
    {
        [JevNoulQuestion("Should the service be restarted?")]
        public JevNoul? ShouldRestart { get; set; }
    }

    private const string ResponseJson = """
        {
          "model": "jev-response-model",
          "answers": {
            "s0": {
              "type": "noul",
              "noul": 0.55
            },
            "s0_c0": {
              "type": "noul",
              "noul": 0.48
            },
            "s0_c1": {
              "type": "noul",
              "noul": 0.06
            },
            "s1": {
              "type": "noul",
              "noul": 0.03
            },
            "s1_c0": {
              "type": "noul",
              "noul": 0.81
            }
          },
          "usage": {
            "input_tokens": 120,
            "output_tokens": 30
          }
        }
        """;

    private const string AttributeResponseJson = """
        {
          "model": "jev-response-model",
          "answers": {
            "ShouldRestart": {
              "type": "noul",
              "noul": 0.9
            }
          },
          "usage": {
            "input_tokens": 10,
            "output_tokens": 2
          }
        }
        """;

    private const string MissingAnswerResponseJson = """
        {
          "model": "jev-response-model",
          "answers": {
            "s0": {
              "type": "noul",
              "noul": 0.55
            },
            "s0_c0": {
              "type": "noul",
              "noul": 0.48
            },
            "s0_c1": {
              "type": "noul",
              "noul": 0.06
            },
            "s1": {
              "type": "noul",
              "noul": 0.03
            }
          },
          "usage": {
            "input_tokens": 10,
            "output_tokens": 2
          }
        }
        """;

    private const string OutOfRangeResponseJson = """
        {
          "model": "jev-response-model",
          "answers": {
            "s0": {
              "type": "noul",
              "noul": 1.5
            }
          },
          "usage": {
            "input_tokens": 10,
            "output_tokens": 2
          }
        }
        """;

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
