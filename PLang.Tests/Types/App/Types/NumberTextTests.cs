using number = global::app.type.item.number.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// A number with a decimal point is a double. Written as text it reads as the asker's culture
/// (<c>%!app.setting.culture%</c>): at most the culture's decimals, trailing zeros dropped, its separator, no grouping,
/// and a non-zero value keeps its first significant digit. Json writes it whole and invariant.
/// </summary>
public class NumberTextTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/number-text-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task Culture(string name)
    {
        var set = await _app.actor.list.User.Setting.Set("app.setting.culture", Ctx.Ok(name));
        await set.IsSuccess();
    }

    private async Task<string> Text(number value)
    {
        using var ms = new System.IO.MemoryStream();
        await global::app.type.item.text.@this.Encode(ms, Ctx.Ok(value), Ctx, null, null, default);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string Json(number value)
    {
        using var ms = new System.IO.MemoryStream();
        using (var utf8 = new System.Text.Json.Utf8JsonWriter(ms))
            value.Write(new global::app.type.item.kind.json.Writer(utf8, emitsSchema: false));
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static number Product => NumberOps.Multiply(number.Parse("2.49")!, (number)3, NumberOps.Lenient);

    [Test]
    public async Task ADecimalPoint_ReadsAsADouble()
        => await Assert.That(number.Parse("2.49")!.Kind.Name).IsEqualTo("double");

    [Test]
    public async Task AProduct_AsText_ShowsTheCulturesDecimals()
    {
        await Culture("en-US");
        await Assert.That(await Text(Product)).IsEqualTo("7.47");
    }

    [Test]
    public async Task AProduct_AsJson_IsWhole()
        => await Assert.That(Json(Product)).IsEqualTo("7.470000000000001");

    [Test]
    public async Task AnIcelandicCulture_WritesItsSeparator()
    {
        await Culture("is-IS");
        await Assert.That(await Text(Product)).IsEqualTo("7,47");
    }

    [Test]
    public async Task ASmallNumber_KeepsItsFirstSignificantDigit()
    {
        await Culture("en-US");
        await Assert.That(await Text(number.Parse("0.001")!)).IsEqualTo("0.001");
    }

    [Test]
    public async Task AWholeNumber_HasNoDecimals()
    {
        await Culture("en-US");
        await Assert.That(await Text((number)3)).IsEqualTo("3");
        await Assert.That(await Text(number.Parse("3.0")!)).IsEqualTo("3");
    }

    // `set %!app.setting.culture% = "xx-YY"`
    [Test]
    public async Task ACultureNoOneHas_FailsAtTheSet()
    {
        var set = await global::PLang.Tests.Shared.Make.Action(Ctx, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(Ctx, "Name", "%!app.setting.culture%", "variable"), ("Value", "xx-YY")).Start(Ctx);
        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("UnknownCulture");
    }

    // `set %!app.setting.culture% = "is-IS"`, then a number written as text
    [Test]
    public async Task ACultureSetInPlang_IsHowNumbersRead()
    {
        var set = await global::PLang.Tests.Shared.Make.Action(Ctx, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(Ctx, "Name", "%!app.setting.culture%", "variable"), ("Value", "is-IS")).Start(Ctx);
        await set.IsSuccess();
        await Assert.That(await Text(Product)).IsEqualTo("7,47");
    }
}
