namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// Type's static Build(value) → kind is the build-time sibling of Resolve.
// Discovered by reflection. Distinct from the action handler's IClass.Build().
// number.Build(3.5)→"decimal", image.Build("a.jpg")→"jpg", path.Build("https://…")→"http".

public class TypeBuildHookTests
{
    private global::app.type.kind.@this _kinds = null!;

    [Before(Test)]
    public void Setup() => _kinds = new global::app.type.kind.@this();

    [Test]
    public async Task TypeBuild_DiscoveredByReflection_LikeResolve()
    {
        // path.@this declares `public static string? Build(object? value)` in
        // this.Build.cs. The dispatcher finds it by reflection (same shape as
        // Resolve discovery) and invokes it.
        await Assert.That(_kinds.Of(typeof(global::app.type.path.@this), "/srv/a.jpg")).IsEqualTo("file");
        await Assert.That(_kinds.Of(typeof(global::app.type.path.@this), "https://x")).IsEqualTo("http");
        await Assert.That(_kinds.Of(typeof(global::app.type.path.@this), "http://x")).IsEqualTo("http");
    }

    [Test]
    public async Task TypeBuild_DistinctFrom_ActionIClassBuild()
    {
        // The type-level Build is `static string? Build(object?)`, not the
        // action's `IClass.Build()` (instance, no params, returns Data).
        // Discovery must filter on the (object?)→string signature only — an
        // action handler that implements IClass.Build() must NOT be picked up
        // as if it were a kind hook.
        var fileReadType = typeof(global::app.module.file.Read);
        await Assert.That(_kinds.Of(fileReadType, "/whatever")).IsNull();
    }

    [Test]
    public async Task TypeBuild_ReturnsNullForUnknownValue_DoesNotThrow()
    {
        // Hook returning null is normal (path.Build returns null for non-strings,
        // empty strings, %var% refs). Dispatcher passes the null through.
        await Assert.That(_kinds.Of(typeof(global::app.type.path.@this), null)).IsNull();
        await Assert.That(_kinds.Of(typeof(global::app.type.path.@this), "")).IsNull();
        await Assert.That(_kinds.Of(typeof(global::app.type.path.@this), 42)).IsNull();
        await Assert.That(_kinds.Of(typeof(global::app.type.path.@this), "%photo%")).IsNull();
    }

    [Test]
    public async Task TypeBuild_NotInvoked_ForTypesWithoutKind_DateTimeDuration()
    {
        // datetime / TimeSpan have no kind concept — no Build hook ⇒ Of returns null.
        await Assert.That(_kinds.Of(typeof(System.DateTime), System.DateTime.UtcNow)).IsNull();
        await Assert.That(_kinds.Of(typeof(System.TimeSpan), System.TimeSpan.FromSeconds(1))).IsNull();
        await Assert.That(_kinds.Of(typeof(string), "hello")).IsNull();
    }
}
