using PLang.Generators;
using PLang.Generators.Discovery;

using PropertyBase = PLang.Generators.Emission.Property.@this;
using DataProperty = PLang.Generators.Emission.Property.Data.@this;

namespace PLang.Tests.Generator;

// Contract tests for IIncrementalGenerator value equality.
// The IIncrementalGenerator pipeline caches by structural equality. If ActionClassInfo
// (the carrier passed across stages) lacked value equality, the cache would always miss
// — every keystroke would re-emit every handler. These tests pin the contract that two
// structurally identical ActionClassInfo instances compare equal AND share a hash code.
//
// Codeanalyzer Finding 1: my prior test only checked for IPropertySymbol leaks; it did
// not check that the cache actually hits. These tests fill that gap.

public class IncrementalCacheTests
{
    private static ActionClassInfo MakeInfo(string name = "Handler",
        params PropertyBase[] props)
        => new(
            Namespace: "App.modules.test",
            ClassName: name,
            FullName: $"App.modules.test.{name}",
            ImplementsIContext: true,
            ImplementsIChannel: false,
            ImplementsIAction: true,
            ImplementsIStep: false,
            ImplementsIStatic: false,
            Properties: new EquatableArray<PropertyBase>(props),
            IEventPropertyNames: EquatableArray<string>.Empty,
            HasAnyIsNotNull: false,
            IsNotNullProperties: EquatableArray<string>.Empty,
            RawScalarValidations: EquatableArray<RawScalarValidation>.Empty,
            Diagnostics: EquatableArray<DiagnosticInfo>.Empty);

    [Test]
    public async Task ActionClassInfo_StructurallyIdentical_AreEqual()
    {
        var a = MakeInfo();
        var b = MakeInfo();
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task ActionClassInfo_DifferentClassName_AreNotEqual()
    {
        var a = MakeInfo("HandlerA");
        var b = MakeInfo("HandlerB");
        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task ActionClassInfo_StructurallyIdenticalProperties_AreEqual()
    {
        var propsA = new PropertyBase[]
        {
            new DataProperty("First", "global::App.Data.@this<string>", IsNullable: false, IsPlainData: false, InnerType: "string", DefaultValue: null),
            new DataProperty("Second", "global::App.Data.@this<int>", IsNullable: false, IsPlainData: false, InnerType: "int", DefaultValue: null),
        };
        var propsB = new PropertyBase[]
        {
            new DataProperty("First", "global::App.Data.@this<string>", IsNullable: false, IsPlainData: false, InnerType: "string", DefaultValue: null),
            new DataProperty("Second", "global::App.Data.@this<int>", IsNullable: false, IsPlainData: false, InnerType: "int", DefaultValue: null),
        };

        var a = MakeInfo("X", propsA);
        var b = MakeInfo("X", propsB);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task ActionClassInfo_DifferentPropertyOrder_AreNotEqual()
    {
        var p1 = new DataProperty("A", "global::App.Data.@this<string>", false, false, "string", null);
        var p2 = new DataProperty("B", "global::App.Data.@this<int>", false, false, "int", null);

        var a = MakeInfo("X", p1, p2);
        var b = MakeInfo("X", p2, p1);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task ActionClassInfo_DifferentMarkers_AreNotEqual()
    {
        var a = MakeInfo();
        var b = a with { ImplementsIChannel = true };
        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task EquatableArray_TwoArraysWithSameElements_Equal()
    {
        var x = new EquatableArray<string>(new[] { "a", "b", "c" });
        var y = new EquatableArray<string>(new[] { "a", "b", "c" });
        await Assert.That(x).IsEqualTo(y);
        await Assert.That(x.GetHashCode()).IsEqualTo(y.GetHashCode());
    }

    [Test]
    public async Task EquatableArray_DifferentElements_NotEqual()
    {
        var x = new EquatableArray<string>(new[] { "a", "b" });
        var y = new EquatableArray<string>(new[] { "a", "c" });
        await Assert.That(x).IsNotEqualTo(y);
    }

    [Test]
    public async Task EquatableArray_DifferentOrder_NotEqual()
    {
        var x = new EquatableArray<string>(new[] { "a", "b" });
        var y = new EquatableArray<string>(new[] { "b", "a" });
        await Assert.That(x).IsNotEqualTo(y);
    }

    [Test]
    public async Task EquatableArray_EmptyAndDefault_Equal()
    {
        var x = new EquatableArray<string>(Array.Empty<string>());
        var y = EquatableArray<string>.Empty;
        await Assert.That(x).IsEqualTo(y);
    }
}
