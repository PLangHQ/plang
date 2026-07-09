using System.Collections.Generic;
using System.Text.Json;
using app.Utils;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Reproduces the ApplyBuiltStep step 0 bug: converting the LLM-response
/// stepResult dict (IDictionary&lt;string, object?&gt;) to Step.@this via
/// TypeMapping. The LLM returns actions, but Step.Actions ends up empty
/// after conversion — so builder.merge copies nothing into the goal step.
/// </summary>
public class StepFromDictConversionTests
{
    private static Dictionary<string, object?> LlmStepDict() => new()
    {
        ["index"] = 0,
        ["actions"] = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["module"] = "builder",
                ["action"] = "validate",
                ["parameters"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Actions",
                        ["value"] = "%stepResult.actions%",
                        ["type"] = "object"
                    }
                }
            },
            new Dictionary<string, object?>
            {
                ["module"] = "error",
                ["action"] = "handle",
                ["parameters"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Goal",
                        ["value"] = new Dictionary<string, object?>
                        {
                            ["name"] = "HandleValidationError"
                        },
                        ["type"] = "goal.call"
                    }
                }
            }
        }
    };

    [Test]
    public async Task Roundtrip_LlmDict_ToStep_ShouldPreserveActions()
    {
        var dict = LlmStepDict();

        // This is exactly the path the builder.merge action takes:
        // %stepResult% is a dict; StepFromLlm is Data<Step>; As<Step>() calls TypeMapping.
        var (converted, err) = TypeMapping.TryConvertTo(dict, typeof(Step));

        await Assert.That(err).IsNull();
        await Assert.That(converted).IsTypeOf<Step>();

        var step = (Step)converted!;
        await Assert.That(step.Index).IsEqualTo(0);
        await Assert.That(step.Actions.Count).IsEqualTo(2); // <-- fails right now
        await Assert.That(step.Actions[0].Module).IsEqualTo("builder");
        await Assert.That(step.Actions[0].ActionName).IsEqualTo("validate");
        await Assert.That(step.Actions[0].Parameters.Count).IsEqualTo(1);
        await Assert.That(step.Actions[0].Parameters[0].Name).IsEqualTo("Actions");
    }

    [Test]
    public async Task Raw_STJ_Serialize_Deserialize_ShouldPreserveActions()
    {
        // Remove TypeMapping from the picture — serialize the dict, deserialize to Step directly.
        // If this succeeds but TryConvertTo doesn't, the bug is in TypeMapping.
        var dict = LlmStepDict();
        var json = JsonSerializer.Serialize(dict);

        var step = JsonSerializer.Deserialize<Step>(json, Json.CaseInsensitiveRead);
        await Assert.That(step).IsNotNull();
        await Assert.That(step!.Actions.Count).IsEqualTo(2);
    }

    /// <summary>
    /// The exact JSON we see from the LLM trace for ApplyBuiltStep step 0 —
    /// parsed the same way the llm.query pipeline would hand it back
    /// (JsonDocument.Parse → JsonElement.Clone → UnwrapJsonElement into dict).
    /// </summary>
    [Test]
    public async Task RealTraceJson_ToStep_ShouldPreserveActions()
    {
        const string json = """
        {
          "index": 0,
          "guidance": "Validate the LLM-generated action set against known modules and parameter schemas. On validation error, call HandleValidationError.",
          "formal": "builder.validate Actions([object] %stepResult.actions%) | error.handle Goal([goal.call] {\"name\":\"HandleValidationError\"})",
          "actions": [
            {
              "module": "builder",
              "action": "validate",
              "parameters": [
                {"name": "Actions", "value": "%stepResult.actions%", "type": "object"}
              ]
            },
            {
              "module": "error",
              "action": "handle",
              "parameters": [
                {"name": "Goal", "value": {"name": "HandleValidationError"}, "type": "goal.call"}
              ]
            }
          ],
          "level": "high",
          "confidence": 90,
          "source": "new"
        }
        """;

        // Reproduce the llm.query path: JsonDocument.Parse → RootElement.Clone()
        // → UnwrapJsonElement (inside Data constructor) → Dictionary<string, object?>
        using var doc = JsonDocument.Parse(json);
        var rootEl = doc.RootElement.Clone();

        // Wrap in Data as llm.query does: data.@this.Ok(resultValue)
        var stepData = global::PLang.Tests.TestApp.SharedContext.Ok(rootEl);
        var dictValue = await stepData.Value(); // UnwrapJsonElement runs in ctor

        // Then As<Step>() to mirror merge.cs: __ResolveData("stepfromllm").Value<Step>()
        var dataStep = stepData.ShallowClone<global::app.type.clr.@this<Step>>(await stepData.Value<global::app.type.clr.@this<Step>>());

        await Assert.That(dataStep.Error).IsNull();
        await Assert.That((await dataStep.Value())).IsNotNull();
        await Assert.That((await dataStep.Value())!.Value.Actions.Count).IsEqualTo(2);
    }
}
