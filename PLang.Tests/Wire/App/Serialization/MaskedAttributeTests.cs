using System.Reflection;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 1
// New [Masked] attribute joins the View.cs attribute cluster.
// Marker only in Stage 1; Stage 2's Normalize walker honors it by emitting "****" for the value.
// Canonical use: setting.value — observable-but-redacted.

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
        var p = typeof(global::app.module.action.setting.type.setting)
            .GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.IsDefined(typeof(global::app.OutAttribute), inherit: true)).IsTrue();
        await Assert.That(p.IsDefined(typeof(global::app.MaskedAttribute), inherit: true)).IsTrue();
    }

    [Test] public async Task SettingValue_Has_OutAndMasked()
    {
        var p = typeof(global::app.module.action.setting.type.setting)
            .GetProperty("value", BindingFlags.Public | BindingFlags.Instance)!;
        await Assert.That(p.IsDefined(typeof(global::app.OutAttribute), inherit: true)).IsTrue();
        await Assert.That(p.IsDefined(typeof(global::app.MaskedAttribute), inherit: true)).IsTrue();
    }

    [Test] public async Task SettingKey_HasOut_NotMasked()
    {
        var p = typeof(global::app.module.action.setting.type.setting)
            .GetProperty("key", BindingFlags.Public | BindingFlags.Instance)!;
        await Assert.That(p.IsDefined(typeof(global::app.OutAttribute), inherit: true)).IsTrue();
        await Assert.That(p.IsDefined(typeof(global::app.MaskedAttribute), inherit: true)).IsFalse();
    }
}
