using System.Reflection;
using app.Attributes;

namespace PLang.Tests.App.TypedReturnsTests;

// Stage 0 — [PlangType] kept as slim Name-only override (Ingi's call during
// implementation diverged from the architect's "remove entirely" plan). These
// tests guard the slim shape: attribute survives, but only the divergent-name
// sites carry the Named form. Most types derive their PLang name from class
// name or @this namespace segment.
// Architect (original intent): .bot/typed-action-returns/architect/stages.md Stage 0 item 1
// Coder handoff (the kept-slim decision): .bot/typed-action-returns/coder/handoff.md §1-3

public class Stage0_PlangTypeRemovalTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // The attribute survives — Ingi's call (handoff §1-3) — but with a single
    // Name parameter only. Shape/Example/Description parameters were dropped;
    // that metadata is now a static-property convention on the type itself.
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

    // Every Named usage must encode a name that the class-name derivation could
    // NOT produce. Two sites today: GoalCall→"goal.call" (dot), Schema.@this→"catalog".
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

    // Results derives "results" from class name lowercased.
    [Test]
    public async Task Results_PlangTypeName_DerivesFromClassNameLowercased()
    {
        var name = _app.Types.Name(typeof(global::app.tester.Results));
        await Assert.That(name).IsEqualTo("results");
    }

    // MockHandle derives "mockhandle" from class name lowercased — the [PlangType]
    // marker on it is the no-arg discoverability form, not a name override.
    [Test]
    public async Task Mock_PlangTypeName_DerivesFromClassName()
    {
        var name = _app.Types.Name(typeof(global::app.modules.mock.types.MockHandle));
        await Assert.That(name).IsEqualTo("mockhandle");
    }

    // After Stage 1 rename, app.tester.Test.@this is an @this class — its PLang
    // type name derives from the last namespace segment ("test"), not the class
    // name (@this). The old name "file" is gone (the File class no longer exists).
    [Test]
    public async Task Test_PlangTypeName_DerivesFromClassName_AfterRename()
    {
        var name = _app.Types.Name(typeof(global::app.tester.Test.@this));
        await Assert.That(name).IsEqualTo("test")
            .Because("@this convention takes the last namespace segment as the PLang type name.");
    }

    // @this classes use the last namespace segment, not the literal "this".
    // Schema.@this uses an explicit override ("catalog") because the segment
    // "Schema" diverges from the desired PLang name.
    [Test]
    public async Task PlangTypeDerivation_OBPSingleNameFolders_UseFolderNameNotThisLiteral()
    {
        var name = _app.Types.Name(typeof(global::app.modules.Schema.@this));
        await Assert.That(name).IsNotEqualTo("this");
        await Assert.That(name).IsEqualTo("catalog")
            .Because("Schema.@this carries [PlangType(\"catalog\")] — the @this segment 'Schema' would not derive to 'catalog'.");
    }
}
