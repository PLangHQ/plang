using Parser = global::app.type.item.variable.parser.@this;

namespace PLang.Tests.App.VariablesTests;

// One of the program's own variables: no `!` name, no binding hop, and its index and method values own too.
// Content read from outside (`read … load vars`) holds only these; each hop answers for itself.
public class VariableOwnTests
{
    [Test]
    [Arguments("%x%")]
    [Arguments("%user.name%")]
    [Arguments("%items[0]%")]
    [Arguments("%items[\"k\"]%")]
    [Arguments("%items[i]%")]
    [Arguments("%items[%i%].name%")]
    [Arguments("%name.replace(\"a\", \"b\")%")]
    [Arguments("%name.replace(%a%, \"b\")%")]
    public async Task TheProgramsOwn(string text)
        => await Assert.That(new Parser(text).Whole!.IsOwn).IsTrue();

    [Test]
    [Arguments("%!app%")]
    [Arguments("%!trace.id%")]
    [Arguments("%!app.type.list.count%")]
    [Arguments("%x!context%")]
    [Arguments("%user.name!type%")]
    [Arguments("%items[%!i%]%")]
    [Arguments("%items[!i]%")]
    [Arguments("%name.replace(%!a%, \"b\")%")]
    [Arguments("%name.replace(\"a\", %x!context%)%")]
    public async Task ReachesOutside(string text)
        => await Assert.That(new Parser(text).Whole!.IsOwn).IsFalse();
}
