using System.Reflection;
using app.Attributes;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 3 — [RequiresCapability] attribute.
/// Pure C# metadata attribute applied to action handler classes. Test discovery
/// reflects on handlers referenced in a test's .pr (and sub-goals) and unions
/// the capabilities into the test's tag set. v1 applications:
///   [RequiresCapability("network")] on http.request / http.download / http.upload
///   [RequiresCapability("llm")]     on llm.query
/// </summary>
public class RequiresCapabilityAttributeTests
{
    [RequiresCapability("network")]
    private sealed class FixtureSingle { }

    [RequiresCapability("network", "auth", "disk")]
    private sealed class FixtureMulti { }

    [RequiresCapability]
    private sealed class FixtureEmpty { }

    // [RequiresCapability("network")] on a class → reflection returns Capabilities == ["network"].
    [Test]
    public async Task Attribute_SingleCapability_ReadableViaReflection()
    {
        var attr = typeof(FixtureSingle).GetCustomAttribute<RequiresCapabilityAttribute>();
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Capabilities.Length).IsEqualTo(1);
        await Assert.That(attr.Capabilities[0]).IsEqualTo("network");
    }

    // [RequiresCapability("network", "auth", "disk")] → Capabilities reflects all three
    // in the order declared.
    [Test]
    public async Task Attribute_MultipleCapabilities_AllReadable()
    {
        var attr = typeof(FixtureMulti).GetCustomAttribute<RequiresCapabilityAttribute>();
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Capabilities).IsEquivalentTo(new[] { "network", "auth", "disk" });
    }

    // [RequiresCapability()] with no args → Capabilities is empty array, not null.
    // Defensive behavior for an attribute authored with zero capabilities. (boundary — independent)
    [Test]
    public async Task Attribute_EmptyParams_CapabilitiesIsEmptyArray()
    {
        var attr = typeof(FixtureEmpty).GetCustomAttribute<RequiresCapabilityAttribute>();
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Capabilities).IsNotNull();
        await Assert.That(attr.Capabilities.Length).IsEqualTo(0);
    }

    // Architect spec: AttributeUsage(AttributeTargets.Class, AllowMultiple = false).
    // Verified via reflection on the attribute's own AttributeUsageAttribute — guards
    // against spec drift if someone adds AttributeTargets.Method or flips AllowMultiple.
    [Test]
    public async Task Attribute_UsageIsClassLevelOnly_AttributeTargetsEnforced()
    {
        var usage = typeof(RequiresCapabilityAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(AttributeTargets.Class);
        await Assert.That(usage.AllowMultiple).IsFalse();
    }

    // Smoke test against real handler classes: http.request, http.download, http.upload
    // carry [RequiresCapability("network")]; llm.query carries [RequiresCapability("llm")].
    // (Test-designer's original plan wrote "llm.ask"; the real action is "llm.query" — same test, correct name.)
    // Catches forgotten applications on future handlers in these module.
    [Test]
    public async Task RealHandlers_HaveExpectedCapabilities_VerifiedByReflection()
    {
        var request = typeof(global::app.module.action.http.request).GetCustomAttribute<RequiresCapabilityAttribute>();
        var download = typeof(global::app.module.action.http.download).GetCustomAttribute<RequiresCapabilityAttribute>();
        var upload = typeof(global::app.module.action.http.upload).GetCustomAttribute<RequiresCapabilityAttribute>();
        var llm = typeof(global::app.module.action.llm.query).GetCustomAttribute<RequiresCapabilityAttribute>();

        await Assert.That(request?.Capabilities).IsEquivalentTo(new[] { "network" });
        await Assert.That(download?.Capabilities).IsEquivalentTo(new[] { "network" });
        await Assert.That(upload?.Capabilities).IsEquivalentTo(new[] { "network" });
        await Assert.That(llm?.Capabilities).IsEquivalentTo(new[] { "llm" });
    }
}
