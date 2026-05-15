using System.Text.Json;
using System.Text.Json.Serialization;
using app;
using global::app.Variables;

namespace PLang.Tests.App.Utils;

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
            Goals = new List<global::app.Goals.Goal.@this> { new() { Name = "SubGoal1" } }
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
        var json = JsonSerializer.Serialize(ErrorOrder.GoalFirst, JsonOptions);

        await Assert.That(json).IsEqualTo("\"goalFirst\"");
    }

    [Test]
    public async Task ErrorOrder_RetryFirst_SerializesCorrectly()
    {
        var json = JsonSerializer.Serialize(ErrorOrder.RetryFirst, JsonOptions);

        await Assert.That(json).IsEqualTo("\"retryFirst\"");
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
                    Actions = new StepActions
                    {
                        new global::app.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "http",
                            ActionName = "get",
                            Parameters = new List<Data> { new Data("url", "https://api.example.com") },
                        },
                        new global::app.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("Name", "response"),
                                new Data("Value", "%__data__%")
                            }
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
        await Assert.That(step.Actions.Count).IsEqualTo(2);
        await Assert.That(step.Actions[0].Module).IsEqualTo("http");
        await Assert.That(step.Actions[0].ActionName).IsEqualTo("get");
        await Assert.That(step.Actions[1].Module).IsEqualTo("variable");
        await Assert.That(step.Actions[1].ActionName).IsEqualTo("set");
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
