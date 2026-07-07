using System.Reflection;
using app.Attributes;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: [PlangType] is a slim Name-only override. Most types derive their
// PLang name from class name (or last namespace segment for @this classes).
// The attribute exists only to override that derivation when the desired
// PLang name can't be encoded in the class name (e.g. dots, fully divergent
// names).

public class Stage0_PlangTypeRemovalTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // The attribute exposes only the Name slot. Shape/Example/Description
    // metadata moved to a static-property convention on the type itself
    // (public static string Example => "...").
    [Test]
    public async Task PlangTypeAttribute_OnlyOverridesDivergentNames()
    {
        var attrType = typeof(PlangTypeAttribute);
        await Assert.That(attrType).IsNotNull();

        var ctors = attrType.GetConstructors();
        var ctorParamCounts = ctors.Select(c => c.GetParameters().Length).OrderBy(n => n).ToList();
        await Assert.That(ctorParamCounts).IsEquivalentTo(new[] { 0, 1 })
            .Because("Only the no-arg marker form and the (string name) divergent-name override survive.");

        var props = attrType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name).ToList();
        await Assert.That(props).IsEquivalentTo(new[] { "Name" })
            .Because("Shape/Example/Description moved to static-property convention.");
    }

    // Every Named usage must encode a name the class-name derivation cannot
    // produce. One legitimate site: GoalCall→"goal.call" (dotted name).
    [Test]
    public async Task PlangType_NoTypeUsesNamedFormForDerivableName()
    {
        var asm = typeof(global::app.@this).Assembly;
        var offenders = new List<string>();

        foreach (var type in asm.GetTypes())
        {
            var attr = type.GetCustomAttribute<PlangTypeAttribute>(inherit: false);
            if (attr == null || attr.Name == null) continue;

            var derivable = type.Name == "this"
                ? (type.Namespace?.Split('.').LastOrDefault() ?? "")
                : type.Name.ToLowerInvariant();

            if (string.Equals(attr.Name, derivable, StringComparison.Ordinal))
                offenders.Add($"{type.FullName} → [PlangType(\"{attr.Name}\")] is identical to derivation");
        }

        await Assert.That(offenders).IsEmpty()
            .Because("Named [PlangType(name)] is reserved for names that can't be derived from class/@this.");
    }


    // app.mock.@this is an @this class — its PLang type name derives
    // from the last namespace segment ("mock"), not the class name ("@this").
    [Test]
    public async Task Mock_PlangTypeName_DerivesFromClassName()
    {
        var name = _app.Type.Name(typeof(global::app.mock.@this));
        await Assert.That(name).IsEqualTo("mock");
    }

    // app.test.@this is a named value type carrying [PlangType("test")] —
    // its PLang name is the explicit attribute value ("test").
    [Test]
    public async Task Test_PlangTypeName_IsExplicitAttributeValue()
    {
        var name = _app.Type.Name(typeof(global::app.test.@this));
        await Assert.That(name).IsEqualTo("test")
            .Because("[PlangType(\"test\")] sets the PLang type name explicitly.");
    }

    // @this classes use the last namespace segment, not the literal "this".
    // app.type.catalog.view.@this → "view" by derivation alone (no override).
    [Test]
    public async Task PlangTypeDerivation_OBPSingleNameFolders_UseFolderNameNotThisLiteral()
    {
        var name = _app.Type.Name(typeof(global::app.type.catalog.view.@this));
        await Assert.That(name).IsNotEqualTo("this");
        await Assert.That(name).IsEqualTo("view")
            .Because("The @this in folder 'view/' derives cleanly to 'view' — no [PlangType] override needed.");
    }
}
