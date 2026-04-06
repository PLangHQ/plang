using System.Text.Json;
using System.Text.Json.Serialization;
using App;
using App.Variables;

namespace PLang.Tests.App.Utility;

public class GoalSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Test]
    public async Task Roundtrip_Goal_PreservesBasicProperties()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Description = "A test goal",
            Comment = "This is a comment",
            Visibility = Visibility.Public,
            IsSetup = true,
            IsEvent = false,
            Hash = "abc123",
            InputParameters = new Dictionary<string, string> { { "param1", "string" } },
            Goals = new List<App.Goals.Goal.@this> { new() { Name = "SubGoal1" } }
        };

        var json = JsonSerializer.Serialize(goal, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Goal>(json, JsonOptions)!;

        await Assert.That(deserialized.Name).IsEqualTo("TestGoal");
        await Assert.That(deserialized.Description).IsEqualTo("A test goal");
        await Assert.That(deserialized.Comment).IsEqualTo("This is a comment");
        await Assert.That(deserialized.Visibility).IsEqualTo(Visibility.Public);
        await Assert.That(deserialized.IsSetup).IsTrue();
        await Assert.That(deserialized.IsEvent).IsFalse();
        await Assert.That(deserialized.Hash).IsEqualTo("abc123");
        await Assert.That(deserialized.InputParameters!["param1"]).IsEqualTo("string");
        await Assert.That(deserialized.Goals[0].Name).IsEqualTo("SubGoal1");
    }

    [Test]
    public async Task Visibility_SerializesAsCamelCaseString()
    {
        var goalPublic = new Goal { Name = "Test", Visibility = Visibility.Public };
        var goalPrivate = new Goal { Name = "Test", Visibility = Visibility.Private };

        var jsonPublic = JsonSerializer.Serialize(goalPublic, JsonOptions);
        var jsonPrivate = JsonSerializer.Serialize(goalPrivate, JsonOptions);

        await Assert.That(jsonPublic).Contains("\"visibility\":\"public\"");
        await Assert.That(jsonPrivate).Contains("\"visibility\":\"private\"");
    }

    [Test]
    public async Task Visibility_DeserializesFromCamelCaseString()
    {
        var json = """{"name":"Test","visibility":"public"}""";
        var goal = JsonSerializer.Deserialize<Goal>(json, JsonOptions)!;

        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Public);
    }

    [Test]
    public async Task ErrorOrder_SerializesAsCamelCaseString()
    {
        var handler = new ErrorHandler { Order = ErrorOrder.GoalFirst };
        var json = JsonSerializer.Serialize(handler, JsonOptions);

        await Assert.That(json).Contains("\"goalFirst\"");
    }

    [Test]
    public async Task ErrorOrder_RetryFirst_SerializesCorrectly()
    {
        var handler = new ErrorHandler { Order = ErrorOrder.RetryFirst };
        var json = JsonSerializer.Serialize(handler, JsonOptions);

        await Assert.That(json).Contains("\"retryFirst\"");
    }

    [Test]
    public async Task Roundtrip_StepsAndActions_Preserved()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "step 1",
                    LineNumber = 5,
                    Indent = 2,
                    Comment = "step comment",
                    WaitForExecution = false,
                    Timeout = 30,
                    Actions = new StepActions
                    {
                        new App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "http",
                            ActionName = "get",
                            Parameters = new List<Data> { new Data("url", "https://api.example.com") },
                            Return = new List<Data> { new Data("response") }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(goal, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Goal>(json, JsonOptions)!;

        await Assert.That(deserialized.Steps.Count).IsEqualTo(1);
        var step = deserialized.Steps[0];
        await Assert.That(step.Index).IsEqualTo(0);
        await Assert.That(step.Text).IsEqualTo("step 1");
        await Assert.That(step.LineNumber).IsEqualTo(5);
        await Assert.That(step.Indent).IsEqualTo(2);
        await Assert.That(step.Comment).IsEqualTo("step comment");
        await Assert.That(step.WaitForExecution).IsFalse();
        await Assert.That(step.Timeout).IsEqualTo(30);
        await Assert.That(step.Actions.Count).IsEqualTo(1);
        await Assert.That(step.Actions[0].Module).IsEqualTo("http");
        await Assert.That(step.Actions[0].ActionName).IsEqualTo("get");
    }

    [Test]
    public async Task Roundtrip_ErrorHandler_Preserved()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "step",
                    OnError = new ErrorHandler
                    {
                        Goal = new GoalCall { Name = "HandleError", Parameters = new List<Data> { new Data("msg", "oops") } },
                        RetryCount = 3,
                        RetryOverMs = 60000,
                        Order = ErrorOrder.RetryFirst,
                        IgnoreError = false,
                        Message = "Something failed",
                        StatusCode = 500,
                        Key = "err1"
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(goal, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Goal>(json, JsonOptions)!;

        var err = deserialized.Steps[0].OnError!;
        await Assert.That(err.Goal!.Name).IsEqualTo("HandleError");
        await Assert.That(err.RetryCount).IsEqualTo(3);
        await Assert.That(err.RetryOverMs).IsEqualTo(60000);
        await Assert.That(err.Order).IsEqualTo(ErrorOrder.RetryFirst);
        await Assert.That(err.IgnoreError).IsFalse();
        await Assert.That(err.Message).IsEqualTo("Something failed");
        await Assert.That(err.StatusCode).IsEqualTo(500);
        await Assert.That(err.Key).IsEqualTo("err1");
    }

    [Test]
    public async Task Roundtrip_CacheSettings_Preserved()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "step",
                    Cache = new CacheSettings
                    {
                        DurationMs = 600_000,
                        Sliding = true,
                        Key = "cache1",
                        Location = "memory"
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(goal, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Goal>(json, JsonOptions)!;

        var cache = deserialized.Steps[0].Cache!;
        await Assert.That(cache.DurationMs).IsEqualTo(600_000);
        await Assert.That(cache.Sliding).IsTrue();
        await Assert.That(cache.Key).IsEqualTo("cache1");
        await Assert.That(cache.Location).IsEqualTo("memory");
    }

    [Test]
    public async Task JsonIgnore_Properties_AreExcluded()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "step" }
            }
        };
        goal.Steps[0].Goal = goal;

        var json = JsonSerializer.Serialize(goal, JsonOptions);

        // Parent, Goal back-ref, and Events should not appear
        await Assert.That(json).DoesNotContain("\"parent\"");
        await Assert.That(json).DoesNotContain("\"events\"");
    }
}
