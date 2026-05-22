using Path = global::app.types.path.@this;
using System;
using System.Threading.Tasks;
// See IPathSchemeFixture.cs — current-type alias, repointed by stage 1's rename sweep.

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// SKELETON — the <see cref="IPathSchemeFixture"/> for the <c>file</c> scheme. The coder
/// implements this in stage 7.
///
/// Required behaviour:
///  - Constructor creates a unique temp directory as the fixture root (and an App rooted
///    there, so minted FilePaths have a Context/Actor for Authorize).
///  - <see cref="CreateFresh"/> mints a <c>FilePath</c> at a unique <c>{guid}.txt</c> under
///    the root, Context-wired (the <c>IContext</c> setter populated). In-root paths
///    auto-grant Authorize, so the round-trip contract tests need no channel.
///  - <see cref="Cleanup"/> deletes the file if present — idempotent.
///  - <see cref="CanPerform"/> returns true for every verb — a filesystem supports them all.
///  - Implement <c>IDisposable</c> if the temp directory needs teardown.
/// </summary>
public sealed class FilePathFixture : IPathSchemeFixture
{
    /// <summary>See class doc — mints a Context-wired FilePath under the temp root.</summary>
    public Task<Path> CreateFresh() => throw new NotImplementedException();

    /// <summary>See class doc — deletes the file if present, idempotent.</summary>
    public Task Cleanup(Path p) => throw new NotImplementedException();

    /// <summary>A filesystem supports every verb.</summary>
    public bool CanPerform(VerbName verb) => true;

    public string Scheme => "file";
}
