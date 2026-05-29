namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// app/data/this.Normalize.cs — at a registered-type value with ≥1 serializer entry,
// Normalize returns TypedValueNode(value, typeName) instead of reflecting.
// Unregistered domain objects reflect exactly as today. The marker is format-agnostic;
// the writer (which knows its own Format) resolves it.

public class TypedValueNodeNormalizeTests
{
    [global::app.Attributes.PlangType("normalize-fixture-with-renderer")]
    public sealed class FixtureWithRenderer
    {
        public string Payload { get; init; } = "";
    }

    [global::app.Attributes.PlangType("normalize-fixture-no-renderer")]
    public sealed class FixtureNoRenderer
    {
        [global::app.LlmBuilder]
        public string Payload { get; init; } = "";
    }

    private global::app.type.list.@this _types = null!;

    [Before(Test)]
    public void Setup()
    {
        _types = new global::app.type.list.@this();
        _types.Assemblies.Add(typeof(TypedValueNodeNormalizeTests).Assembly);
        _types.Renderers.Assemblies.Add(typeof(TypedValueNodeNormalizeTests).Assembly);
        _types.Renderers.Register("normalize-fixture-with-renderer",
            global::app.type.renderer.@this.AnyFormat,
            (v, w) => w.String("rendered"));
    }

    [Test]
    public async Task Normalize_RegisteredType_ReturnsTypedValueNode()
    {
        var fixture = new FixtureWithRenderer { Payload = "p" };
        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var result = global::app.data.@this.NormalizeValue(fixture, global::app.View.Out, visited, 0, _types);
        await Assert.That(result).IsTypeOf<global::app.data.TypedValueNode>();
        var node = (global::app.data.TypedValueNode)result!;
        await Assert.That(node.TypeName).IsEqualTo("normalize-fixture-with-renderer");
        await Assert.That(node.Value).IsEqualTo(fixture);
    }

    [Test]
    public async Task Normalize_UnregisteredType_ReflectsAsBefore()
    {
        // No [PlangType] ⇒ no name resolution ⇒ falls to reflection (property bag).
        var pojo = new { Foo = "bar" };
        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var result = global::app.data.@this.NormalizeValue(pojo, global::app.View.Out, visited, 0, _types);
        await Assert.That(result).IsNotTypeOf<global::app.data.TypedValueNode>();
    }

    [Test]
    public async Task TypedValueNode_CarriesValueAndTypeName_NoFormatToken()
    {
        var node = new global::app.data.TypedValueNode("v", "t");
        await Assert.That(node.Value).IsEqualTo("v");
        await Assert.That(node.TypeName).IsEqualTo("t");
        // Sealed record exposes only Value + TypeName — no Format, no Writer reference.
        var props = typeof(global::app.data.TypedValueNode).GetProperties()
            .Select(p => p.Name).ToHashSet();
        await Assert.That(props).Contains("Value");
        await Assert.That(props).Contains("TypeName");
        await Assert.That(props).DoesNotContain("Format");
    }

    [Test]
    public async Task Normalize_RegisteredTypeWithNoSerializer_ReflectsAsBefore()
    {
        // [PlangType] but no renderer ⇒ Has(typeName)=false ⇒ reflection walk.
        var fixture = new FixtureNoRenderer { Payload = "p" };
        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var result = global::app.data.@this.NormalizeValue(fixture, global::app.View.Out, visited, 0, _types);
        await Assert.That(result).IsNotTypeOf<global::app.data.TypedValueNode>();
    }

    [Test]
    public async Task Normalize_NestedRegisteredValueInsideUnregistered_TagsInner()
    {
        // An unregistered outer with a registered inner — outer reflects,
        // inner emerges as TypedValueNode through the reflection walk.
        var outer = new { Inner = new FixtureWithRenderer { Payload = "p" } };
        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var result = global::app.data.@this.NormalizeValue(outer, global::app.View.Out, visited, 0, _types);
        // The outer becomes a property bag List<Data>; the Inner child's value
        // is the TypedValueNode.
        await Assert.That(result).IsTypeOf<List<global::app.data.@this>>();
        var bag = (List<global::app.data.@this>)result!;
        var innerChild = bag.FirstOrDefault(d => d.Name == "inner");
        await Assert.That(innerChild).IsNotNull();
        await Assert.That(innerChild!.Value).IsTypeOf<global::app.data.TypedValueNode>();
    }

    [Test]
    public async Task TypedValueNode_IsSealedRecord_ValueEquality()
    {
        var a = new global::app.data.TypedValueNode("v", "t");
        var b = new global::app.data.TypedValueNode("v", "t");
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(typeof(global::app.data.TypedValueNode).IsSealed).IsTrue();
    }
}
