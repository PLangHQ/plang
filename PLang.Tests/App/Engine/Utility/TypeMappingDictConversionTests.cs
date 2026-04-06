using App.Engine.Variables;
using App.Engine.Utility;

namespace PLang.Tests.App.Engine.Utility;

public class TypeMappingDictConversionTests
{
    [Test]
    public async Task TryConvertTo_DictToClass_SetsProperties()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Age"] = 30
        };

        var (result, error) = TypeMapping.TryConvertTo(dict, typeof(SimpleTarget));

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
        var target = result as SimpleTarget;
        await Assert.That(target!.Name).IsEqualTo("Alice");
        await Assert.That(target!.Age).IsEqualTo(30);
    }

    [Test]
    public async Task TryConvertTo_DictToClass_CaseInsensitive()
    {
        var dict = new Dictionary<string, object?>
        {
            ["name"] = "Bob",
            ["age"] = 25
        };

        var (result, error) = TypeMapping.TryConvertTo(dict, typeof(SimpleTarget));

        await Assert.That(error).IsNull();
        var target = result as SimpleTarget;
        await Assert.That(target!.Name).IsEqualTo("Bob");
        await Assert.That(target!.Age).IsEqualTo(25);
    }

    [Test]
    public async Task TryConvertTo_DictToStep_Works()
    {
        var dict = new Dictionary<string, object?>
        {
            ["index"] = 0,
            ["approved"] = false,
            ["actions"] = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["module"] = "goal",
                    ["action"] = "call",
                    ["parameters"] = new List<object>
                    {
                        new Dictionary<string, object?> { ["name"] = "GoalName", ["value"] = "Test", ["type"] = "goal.call" }
                    }
                }
            },
            ["onError"] = new Dictionary<string, object?>
            {
                ["retryCount"] = 2,
                ["order"] = "RetryFirst",
                ["goal"] = new Dictionary<string, object?> { ["name"] = "HandleError" }
            }
        };

        var (result, error) = TypeMapping.TryConvertTo(dict, typeof(Step));

        // Report what happened
        if (error != null)
            Assert.Fail($"Conversion failed: {error.Message}");

        await Assert.That(result).IsNotNull();
        var step = result as Step;
        await Assert.That(step!.Actions.Count).IsEqualTo(1);
        await Assert.That(step!.OnError).IsNotNull();
        await Assert.That(step!.OnError!.RetryCount).IsEqualTo(2);
    }

    [Test]
    public async Task TryConvertTo_DictToClass_ConvertsNestedValues()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Name"] = "Charlie",
            ["Age"] = "35" // string that needs int conversion
        };

        var (result, error) = TypeMapping.TryConvertTo(dict, typeof(SimpleTarget));

        await Assert.That(error).IsNull();
        var target = result as SimpleTarget;
        await Assert.That(target!.Age).IsEqualTo(35);
    }

    [Test]
    public async Task TryConvertTo_DictToClass_IgnoresExtraKeys()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Name"] = "Dave",
            ["Age"] = 40,
            ["ExtraField"] = "ignored"
        };

        var (result, error) = TypeMapping.TryConvertTo(dict, typeof(SimpleTarget));

        await Assert.That(error).IsNull();
        var target = result as SimpleTarget;
        await Assert.That(target!.Name).IsEqualTo("Dave");
    }

    [Test]
    public async Task TryConvertTo_DictToClass_WithNestedDict()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Text"] = "do something",
            ["OnError"] = new Dictionary<string, object?>
            {
                ["RetryCount"] = 2,
                ["Goal"] = new Dictionary<string, object?>
                {
                    ["Name"] = "HandleError"
                }
            }
        };

        var (result, error) = TypeMapping.TryConvertTo(dict, typeof(StepLike));

        await Assert.That(error).IsNull();
        var target = result as StepLike;
        await Assert.That(target!.Text).IsEqualTo("do something");
        await Assert.That(target!.OnError).IsNotNull();
        await Assert.That(target!.OnError!.RetryCount).IsEqualTo(2);
        await Assert.That(target!.OnError!.Goal).IsNotNull();
        await Assert.That(target!.OnError!.Goal!.Name).IsEqualTo("HandleError");
    }

    [Test]
    public async Task TryConvertTo_DictToClass_WithList()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Text"] = "test step",
            ["Actions"] = new List<object>
            {
                new Dictionary<string, object?> { ["Module"] = "file", ["ActionName"] = "read" },
                new Dictionary<string, object?> { ["Module"] = "output", ["ActionName"] = "write" }
            }
        };

        var (result, error) = TypeMapping.TryConvertTo(dict, typeof(StepLike));

        await Assert.That(error).IsNull();
        var target = result as StepLike;
        await Assert.That(target!.Actions).IsNotNull();
        await Assert.That(target!.Actions!.Count).IsEqualTo(2);
        await Assert.That(target!.Actions![0].Module).IsEqualTo("file");
        await Assert.That(target!.Actions![1].Module).IsEqualTo("output");
    }
}

public class SimpleTarget
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

public class StepLike
{
    public string Text { get; set; } = "";
    public ErrorHandlerLike? OnError { get; set; }
    public List<ActionLike>? Actions { get; set; }
}

public class ErrorHandlerLike
{
    public int RetryCount { get; set; }
    public GoalRefLike? Goal { get; set; }
}

public class GoalRefLike
{
    public string Name { get; set; } = "";
}

public class ActionLike
{
    public string Module { get; set; } = "";
    public string ActionName { get; set; } = "";
}
