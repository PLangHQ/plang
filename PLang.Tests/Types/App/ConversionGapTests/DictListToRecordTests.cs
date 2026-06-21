namespace PLang.Tests.App.Types.ConversionGapTests;

using Data = global::app.data.@this;
using Catalog = global::app.type.catalog.@this;
using LlmMessage = global::app.module.llm.LlmMessage;

/// <summary>
/// Isolates the born-type gap behind the builder failure: a list of dicts
/// (list&lt;dict&lt;string,data&gt;&gt;) converting to a list of typed records
/// (list&lt;llmmessage&gt;). No builder/.pr confounders — just the conversion path.
/// </summary>
public class DictListToRecordTests
{
    private static global::app.type.dict.@this Dict(string role, string content)
    {
        var d = new global::app.type.dict.@this();
        d.Set("Role", role);
        d.Set("Content", content);
        return d;
    }

    [Test]
    public async Task ListOfDicts_To_ListOfLlmMessage()
    {
        var lst = new global::app.type.list.@this();
        lst.Add(new Data("", Dict("system", "sys-msg")));
        lst.Add(new Data("", Dict("user", "hi")));

        var (value, error) = Catalog.TryConvert(
            lst, typeof(global::app.type.list.@this<LlmMessage>), null);

        await Assert.That(error?.Message).IsNull();
        var typed = value as global::app.type.list.@this<LlmMessage>;
        await Assert.That(typed).IsNotNull();
        await Assert.That(typed!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task JsonString_To_ListOfLlmMessage()
    {
        var json = "[{\"Role\":\"system\",\"Content\":\"sys-msg\"},{\"Role\":\"user\",\"Content\":\"hi\"}]";
        var (value, error) = Catalog.TryConvert(
            json, typeof(global::app.type.list.@this<LlmMessage>), null);

        await Assert.That(error?.Message).IsNull();
        var typed = value as global::app.type.list.@this<LlmMessage>;
        await Assert.That(typed).IsNotNull();
        await Assert.That(typed!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task JsonArray_To_ListOfLlmMessage()
    {
        // `set %x% = [...], type=json` stores a JsonNode (per the builder runbook) —
        // this is the actual source shape the builder feeds llm.query.
        var arr = System.Text.Json.Nodes.JsonNode.Parse(
            "[{\"Role\":\"system\",\"Content\":\"sys-msg\"},{\"Role\":\"user\",\"Content\":\"hi\"}]");
        var (value, error) = Catalog.TryConvert(
            arr, typeof(global::app.type.list.@this<LlmMessage>), null);

        await Assert.That(error?.Message).IsNull();
        var typed = value as global::app.type.list.@this<LlmMessage>;
        await Assert.That(typed).IsNotNull();
        await Assert.That(typed!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SingleDict_To_LlmMessage()
    {
        var (value, error) = Catalog.TryConvert(Dict("user", "hi"), typeof(LlmMessage), null);
        await Assert.That(error?.Message).IsNull();
        await Assert.That(value as LlmMessage).IsNotNull();
        await Assert.That((value as LlmMessage)!.Role).IsEqualTo("user");
    }
}
