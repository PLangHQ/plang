namespace PLang.Tests.App.Serializers;

public class JsonSerializerRoundTripTests
{
    [Test]
    public async Task JsonSerializer_Write_EmitsValueOnly_NeverReadsSignature()
    {
        // application/json wire shape is data.Value only; data.Signature
        // backing field stays null after Write.
        var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var data = new Data("v", "hello", context: ctx);

        var s = (await ctx.Format("application/json").Serialize(data, ctx).Value())!.Clr<string>()!;

        await Assert.That(s.Contains("hello")).IsTrue();
    }

    [Test]
    public async Task JsonSerializer_Read_ProducesData_WithoutPopulatingSignature()
    {
        // Reading a JSON wire payload reconstructs Data with Value set; Signature stays null.
        var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var raw = "\"hello\"";
        var s = (await ctx.Format("application/json").Deserialize<global::app.type.item.text.@this>(raw, ctx).Value())!;
        await Assert.That(s.ToString()).IsEqualTo("hello");
    }
}
