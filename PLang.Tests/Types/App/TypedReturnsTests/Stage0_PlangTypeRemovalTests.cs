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

    // Nothing is guessed from a folder: an @this type that declares no word goes by its namespace, and one
    // that declares a word goes by that word — whatever its folder's last segment is.
    [Test]
    public async Task AType_GoesByItsDeclaredWord_ElseItsNamespace()
    {
        var context = _app.actor.list.User.Context;
        await Assert.That(_app.type.list["app.event"].Name).IsEqualTo("app.event");
        await Assert.That(_app.type.list["text"].Namespace).IsEqualTo("app.type.item.text");
        await Assert.That(_app.type.list["app.type.item.text"].Name).IsEqualTo("text");
        await Assert.That(_app.type.list.Contains("event")).IsFalse();
        await Assert.That(new global::app.type.@this(typeof(global::app.@event.on.@this)).Name).IsEqualTo("app.event.on");
    }

    // Every word, alias and namespace names one type.
    [Test]
    public async Task EveryWordAliasAndNamespace_NamesOneType()
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clashes = new List<string>();
        for (var i = 0; i < _app.type.list.CountRaw; i++)
        {
            var type = (global::app.type.@this)_app.type.list.At(i, _app.actor.list.User.Context)!.Peek()!;
            foreach (var claim in new[] { type.Name, type.Namespace }.Concat(type.Alias).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
                if (owners.TryGetValue(claim, out var owner) && owner != type.Namespace) clashes.Add($"{claim}: {owner} and {type.Namespace}");
                else owners[claim] = type.Namespace ?? type.Name;
        }
        await Assert.That(clashes).IsEmpty();
    }


    // app.mock.@this is an @this class — its PLang type name derives
    // from the last namespace segment ("mock"), not the class name ("@this").
    [Test]
    public async Task Mock_PlangTypeName_DerivesFromClassName()
    {
        var name = _app.type.list[typeof(global::app.@event.binding.mock.@this)].ToString();
        await Assert.That(name).IsEqualTo("mock");
    }

    // app.test.@this is a named value type carrying [PlangType("test")] —
    // its PLang name is the explicit attribute value ("test").
    [Test]
    public async Task Test_PlangTypeName_IsExplicitAttributeValue()
    {
        var name = _app.type.list[typeof(global::app.test.@this)].ToString();
        await Assert.That(name).IsEqualTo("test")
            .Because("[PlangType(\"test\")] sets the PLang type name explicitly.");
    }

    // @this classes use the last namespace segment, not the literal "this".
    // app.type.item.guid.@this → "guid" by derivation alone (no override).
    [Test]
    public async Task PlangTypeDerivation_OBPSingleNameFolders_UseFolderNameNotThisLiteral()
    {
        var name = _app.type.list[typeof(global::app.type.item.guid.@this)].ToString();
        await Assert.That(name).IsNotEqualTo("this");
        await Assert.That(name).IsEqualTo("guid")
            .Because("The @this in folder 'guid/' derives cleanly to 'guid' — no [PlangType] override needed.");
    }
}
