using Path = global::app.type.item.path.@this;
using System.Threading.Tasks;
// Path alias points at the CURRENT type so this file compiles before stage 1.
// Stage 1's `app.filesystem` → `app.type.item.path` rename sweep repoints it to
// `app.type.item.path.@this` (the abstract base) — same treatment as the existing
// PathAuthorizeTests / FileHandlerTests aliases.

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// The fixture contract every scheme provides so it can be run through
/// <see cref="PathSchemeContractTests{TFixture}"/>. Implementing this interface plus a
/// one-line <c>: PathSchemeContractTests&lt;MyFixture&gt;</c> subclass is the entire cost
/// of putting a new scheme (S3, Git, ...) under the full verb + permission contract suite.
///
/// A fixture mints fresh, writable <see cref="Path"/> instances for its scheme and tears
/// them down. A path holds no context; the
/// fixture's <see cref="Context"/> is the caller every verb passes, whose actor Authorize checks.
/// </summary>
public interface IPathSchemeFixture
{
    /// <summary>
    /// Mints a fresh, writable Path unique to this call, ready for verb calls. Each call returns a distinct resource so tests do not collide.
    /// </summary>
    Task<Path> CreateFresh();

    /// <summary>The caller's context — the actor whose permission every verb checks.</summary>
    global::app.actor.context.@this Context { get; }

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
