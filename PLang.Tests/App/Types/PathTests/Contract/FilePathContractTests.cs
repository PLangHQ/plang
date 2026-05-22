using TUnit.Core;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Stage 7 — runs the full <see cref="PathSchemeContractTests{TFixture}"/> suite against the
/// <c>file</c> scheme. The body is intentionally empty: every <c>[Test]</c> is inherited
/// from the generic base. <c>[InheritsTests]</c> is required for TUnit to discover the
/// inherited tests on this closed concrete subclass.
/// </summary>
[InheritsTests]
public sealed class FilePathContractTests : PathSchemeContractTests<FilePathFixture>
{
}
