using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Decision 4 — no content sniffing. A guess at json/xml/yaml/csv
// contradicts "the type reads itself" and PLang's determinism. These rows
// are deliberately *negative*: each row picks a content shape and asserts
// it is NOT auto-typed.
public class NoContentSniffingTests
{
    // A `{` prefix is not enough to auto-pick json. Without `as json`,
    // the value stays untyped and navigation errors.
    [Test] public async Task Reader_DoesNotSniffJsonByLookingForLeadingBrace() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Reader_DoesNotSniffXmlByLookingForAngleBracket() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Reader_DoesNotSniffCsvByLookingForCommas() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Reader_DoesNotSniffYamlByLookingForColon() { throw new System.NotImplementedException("not implemented"); }
}
