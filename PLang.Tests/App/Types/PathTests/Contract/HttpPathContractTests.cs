using TUnit.Core;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Runs the full <see cref="PathSchemeContractTests{TFixture}"/> suite against the
/// <c>http</c> scheme. Empty body: every <c>[Test]</c> is inherited from the generic base
/// (<c>[InheritsTests]</c> makes TUnit discover them). HttpPath passing the SAME contract
/// suite as FilePath is the proof that path polymorphism holds — a non-filesystem scheme
/// satisfies the identical verb + permission contract.
/// </summary>
[InheritsTests]
public sealed class HttpPathContractTests : PathSchemeContractTests<HttpPathFixture>
{
}
