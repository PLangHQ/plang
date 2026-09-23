using System.Reflection;

namespace PLang.Tests.App.Serialization;

// [Masked] — observable-but-redacted: the property rides the wire as "****" and is stored for real.
// Fixture: MaskedItem (test-only).

public class MaskedAttributeTests
{
    [Test] public async Task MaskedAttribute_Exists_InAppNamespace()
    {
        var t = typeof(global::app.MaskedAttribute);
        await Assert.That(t.Namespace).IsEqualTo("app");
        await Assert.That(t.Name).IsEqualTo("MaskedAttribute");
    }

    [Test] public async Task MaskedAttribute_IsSealed_PropertyTargetOnly()
    {
        var t = typeof(global::app.MaskedAttribute);
        await Assert.That(t.IsSealed).IsTrue();
        var usage = t.GetCustomAttribute<AttributeUsageAttribute>();
        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(AttributeTargets.Property);
    }

    [Test] public async Task MaskedAttribute_CanCoexistWithOut_OnSameProperty()
    {
        var p = typeof(MaskedItem).GetProperty("value", BindingFlags.Public | BindingFlags.Instance)!;
        await Assert.That(p.IsDefined(typeof(global::app.OutAttribute), inherit: true)).IsTrue();
        await Assert.That(p.IsDefined(typeof(global::app.MaskedAttribute), inherit: true)).IsTrue();
    }

    private static async Task<string> Written(global::app.View view)
    {
        var app = TestApp.Create("/test");
        var serializer = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetOrDefault("application/plang");
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, new MaskedItem { key = "ApiKey", value = "sk-real-secret" }, view);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test] public async Task Wire_MaskedValue_WritesFourStars_KeyVisible()
    {
        var json = await Written(global::app.View.Out);
        await Assert.That(json).Contains("ApiKey");
        await Assert.That(json).Contains("****");
        await Assert.That(json).DoesNotContain("sk-real-secret");
    }

    [Test] public async Task Store_MaskedValue_WritesRealValue()
    {
        var json = await Written(global::app.View.Store);
        await Assert.That(json).Contains("sk-real-secret");
    }
}
