using global::App.Utils;

namespace PLang.Tests.App.Memory;

using R2 = global::App.Data;

public class TStringTests
{
    [Test]
    public async Task Constructor_SetsValue()
    {
        var ts = new R2.TString("hello");
        await Assert.That(ts.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task Constructor_SetsKey()
    {
        var ts = new R2.TString("hello", "greeting.hello");
        await Assert.That(ts.Key).IsEqualTo("greeting.hello");
    }

    [Test]
    public async Task ToString_NoResolver_ReturnsRawValue()
    {
        var ts = new R2.TString("hello %name%");
        await Assert.That(ts.ToString()).IsEqualTo("hello %name%");
    }

    [Test]
    public async Task ToString_WithResolver_ResolvesVariables()
    {
        var vars = new Dictionary<string, object?> { ["name"] = "John" };
        var ts = new R2.TString("hello %name%", resolver: name => vars.GetValueOrDefault(name));

        await Assert.That(ts.ToString()).IsEqualTo("hello John");
    }

    [Test]
    public async Task ToString_MultipleVariables()
    {
        var vars = new Dictionary<string, object?>
        {
            ["first"] = "Jane",
            ["last"] = "Doe"
        };
        var ts = new R2.TString("Hello %first% %last%!", resolver: name => vars.GetValueOrDefault(name));

        await Assert.That(ts.ToString()).IsEqualTo("Hello Jane Doe!");
    }

    [Test]
    public async Task ToString_UnresolvedVariable_KeptAsIs()
    {
        var vars = new Dictionary<string, object?> { ["name"] = "John" };
        var ts = new R2.TString("hello %name%, age %age%", resolver: name => vars.GetValueOrDefault(name));

        await Assert.That(ts.ToString()).IsEqualTo("hello John, age %age%");
    }

    [Test]
    public async Task ToString_NoVariables_ReturnsPlainText()
    {
        var ts = new R2.TString("hello world", resolver: _ => null);

        await Assert.That(ts.ToString()).IsEqualTo("hello world");
    }

    [Test]
    public async Task ToString_EmptyPercent_PreservesLiteral()
    {
        var ts = new R2.TString("100%%", resolver: _ => null);

        await Assert.That(ts.ToString()).IsEqualTo("100%%");
    }

    [Test]
    public async Task ToString_UnclosedPercent_PreservesAsIs()
    {
        var ts = new R2.TString("50% done", resolver: _ => null);

        await Assert.That(ts.ToString()).IsEqualTo("50% done");
    }

    [Test]
    public async Task ToString_DotNotation_PassedToResolver()
    {
        var ts = new R2.TString("name: %user.name%", resolver: name =>
            name == "user.name" ? "Alice" : null);

        await Assert.That(ts.ToString()).IsEqualTo("name: Alice");
    }

    [Test]
    public async Task ToString_NullResolverResult_KeepsPlaceholder()
    {
        var ts = new R2.TString("hello %missing%", resolver: _ => null);

        await Assert.That(ts.ToString()).IsEqualTo("hello %missing%");
    }

    [Test]
    public async Task ImplicitFromString()
    {
        R2.TString ts = "hello";
        await Assert.That(ts.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task ImplicitToString()
    {
        var ts = new R2.TString("hello");
        string s = ts;
        await Assert.That(s).IsEqualTo("hello");
    }

    [Test]
    public async Task Equals_SameValue_True()
    {
        var ts1 = new R2.TString("hello");
        var ts2 = new R2.TString("hello");
        await Assert.That(ts1.Equals(ts2)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentValue_False()
    {
        var ts1 = new R2.TString("hello");
        var ts2 = new R2.TString("world");
        await Assert.That(ts1.Equals(ts2)).IsFalse();
    }

    [Test]
    public async Task Equals_String_True()
    {
        var ts = new R2.TString("hello");
        await Assert.That(ts.Equals("hello")).IsTrue();
    }

    [Test]
    public async Task TypeMapping_ResolvesTString()
    {
        var type = TypeMapping.GetType("tstring");
        await Assert.That(type).IsEqualTo(typeof(R2.TString));
    }

    [Test]
    public async Task TypeMapping_ResolvesTranslatable()
    {
        var type = TypeMapping.GetType("translatable");
        await Assert.That(type).IsEqualTo(typeof(R2.TString));
    }

    [Test]
    public async Task TypeMapping_ReverseLookup()
    {
        var name = TypeMapping.GetTypeName(typeof(R2.TString));
        await Assert.That(name).IsEqualTo("tstring");
    }

    [Test]
    public async Task GetHashCode_SameForEqualValues()
    {
        var ts1 = new R2.TString("hello");
        var ts2 = new R2.TString("hello");
        await Assert.That(ts1.GetHashCode()).IsEqualTo(ts2.GetHashCode());
    }
}
