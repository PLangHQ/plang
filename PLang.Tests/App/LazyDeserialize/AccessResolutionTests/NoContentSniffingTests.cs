using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Decision 4 — no content sniffing. A guess at json/xml/yaml/csv
// contradicts "the type reads itself" and PLang's determinism. These rows
// are deliberately *negative*: each row picks a content shape and asserts
// it is NOT auto-typed. An authored string with a structured-looking shape
// stays a string — the value is only parsed when a type names how.
public class NoContentSniffingTests
{
    // A `{` prefix is not enough to auto-pick json. Without `as json`,
    // the value stays the string and is never materialized to a dict.
    [Test] public async Task Reader_DoesNotSniffJsonByLookingForLeadingBrace()
    {
        var d = data.Ok("{\"a\":1}");
        await Assert.That(d.Value).IsEqualTo((object)"{\"a\":1}");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    [Test] public async Task Reader_DoesNotSniffXmlByLookingForAngleBracket()
    {
        var d = data.Ok("<root><a>1</a></root>");
        await Assert.That(d.Value).IsEqualTo((object)"<root><a>1</a></root>");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    [Test] public async Task Reader_DoesNotSniffCsvByLookingForCommas()
    {
        var d = data.Ok("a,b,c");
        await Assert.That(d.Value).IsEqualTo((object)"a,b,c");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    [Test] public async Task Reader_DoesNotSniffYamlByLookingForColon()
    {
        var d = data.Ok("key: value");
        await Assert.That(d.Value).IsEqualTo((object)"key: value");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }
}
