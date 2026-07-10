using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// The `IKindValidatable` marker. Sibling to IBooleanResolvable in
// app/data/. The marker is the seam strict uses in ValidateBuild — the
// design lives here even before image implements the byte-sniff body.
public class IKindValidatableMarkerTests
{
    [Test] public async Task Marker_Defined_InAppDataNamespace()
    {
        var t = typeof(global::app.data.IKindValidatable);
        await Assert.That(t.IsInterface).IsTrue();
        await Assert.That(t.Namespace).IsEqualTo("app.data");
    }

    [Test] public async Task Marker_Signature_BoolAndActualKindTuple()
    {
        var t = typeof(global::app.data.IKindValidatable);
        var m = t.GetMethod("ValidateKind", BindingFlags.Public | BindingFlags.Instance)!;
        await Assert.That(m).IsNotNull();
        var ps = m.GetParameters();
        await Assert.That(ps.Length).IsEqualTo(2);
        await Assert.That(ps[0].ParameterType).IsEqualTo(typeof(object));
        await Assert.That(ps[1].ParameterType).IsEqualTo(typeof(string));
        // Return is a (bool ok, string? actualKind) tuple.
        await Assert.That(m.ReturnType).IsEqualTo(typeof(System.ValueTuple<bool, string?>));
    }

    [Test] public async Task Image_ImplementsIKindValidatable()
    {
        var image = typeof(global::app.type.item.image.@this);
        await Assert.That(typeof(global::app.data.IKindValidatable).IsAssignableFrom(image)).IsTrue();
    }

    [Test] public async Task Text_DoesNotImplementIKindValidatable()
    {
        // Pinned: no app.type.item.text.@this exists yet (Stage 2 lands it).
        // The negative shape is: no CLR type under `app.type.item.text` implements
        // the marker. Verify by walking the PLang assembly for any
        // `app.type.item.text.*` types and asserting none implement IKindValidatable.
        var asm = typeof(global::app.type.@this).Assembly;
        var textImpls = asm.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("app.type.item.text", System.StringComparison.Ordinal))
            .Where(t => typeof(global::app.data.IKindValidatable).IsAssignableFrom(t))
            .ToList();
        await Assert.That(textImpls.Count).IsEqualTo(0);
    }

    [Test] public async Task Number_DoesNotImplementIKindValidatable()
    {
        var number = typeof(global::app.type.item.number.@this);
        await Assert.That(typeof(global::app.data.IKindValidatable).IsAssignableFrom(number)).IsFalse();
    }
}
