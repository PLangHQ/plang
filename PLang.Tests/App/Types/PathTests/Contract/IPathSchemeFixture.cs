using System.Threading.Tasks;
// Path alias points at the CURRENT type so this file compiles before stage 1.
// Stage 1's `app.filesystem` → `app.types.path` rename sweep repoints it to
// `app.types.path.@this` (the abstract base) — same treatment as the existing
// PathAuthorizeTests / FileHandlerTests aliases.
using Path = global::app.filesystem.path;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Stage 7 — the fixture contract every scheme provides so it can be run through
/// <see cref="PathSchemeContractTests{TFixture}"/>. Implementing this interface plus a
/// one-line <c>: PathSchemeContractTests&lt;MyFixture&gt;</c> subclass is the entire cost
/// of putting a new scheme (S3, Git, ...) under the full verb + permission contract suite.
///
/// A fixture mints fresh, writable <see cref="Path"/> instances for its scheme and tears
/// them down. The Path it returns from <see cref="CreateFresh"/> MUST already be
/// Context-wired (the <c>IContext</c> setter populated with an Actor/App) — the contract
/// suite calls verbs that hit Authorize, which needs <c>Context.Actor</c>. The
/// scheme-subclass constructor is <c>(string raw)</c> only, so wiring Context is the
/// fixture's job, not the constructor's.
/// </summary>
public interface IPathSchemeFixture
{
    /// <summary>
    /// Mints a fresh, writable Path unique to this call, already Context-wired and ready
    /// for verb calls. Each call returns a distinct resource so tests do not collide.
    /// </summary>
    Task<Path> CreateFresh();

    /// <summary>
    /// Tears down the resource at <paramref name="p"/>. Idempotent — deleting an
    /// already-gone resource is fine. Called from a <c>finally</c> in every contract test.
    /// </summary>
    Task Cleanup(Path p);

    /// <summary>
    /// True if this scheme's backing system implements <paramref name="verb"/> as a
    /// concept at all. Used to SCOPE which contract tests run — NOT to skip assertions. A
    /// server that answers 405 for a verb still "can perform" it (405 is a return value
    /// the suite asserts on); <c>CanPerform</c> returns false only for a genuine
    /// "no such concept" case (e.g. an HTTP fixture with no directory-listing route).
    /// </summary>
    bool CanPerform(VerbName verb);

    /// <summary>The scheme name this fixture provides — for diagnostic output.</summary>
    string Scheme { get; }
}
