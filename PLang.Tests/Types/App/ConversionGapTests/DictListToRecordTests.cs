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
        var d = new global::app.type.dict.@this(global::PLang.Tests.TestApp.SharedContext);
        d.Set("Role", role);
        d.Set("Content", content);
        return d;
    }

    [Test]
    public async Task ListOfDicts_To_ListOfLlmMessage()
    {
        // The live path: a list bound to a list<T> slot re-tags (Value<list<T>>);
        // each row stays a dict, converting to T only when read.
        var lst = new global::app.type.list.@this();
        lst.Add(new Data("", Dict("system", "sys-msg")));
        lst.Add(new Data("", Dict("user", "hi")));

        var typed = await new Data("", lst).Value<global::app.type.list.@this<LlmMessage>>();

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
