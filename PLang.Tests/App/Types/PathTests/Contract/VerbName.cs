namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Test-only enum naming the verbs a <see cref="IPathSchemeFixture"/> reports on
/// via <see cref="IPathSchemeFixture.CanPerform"/>. Not a production type — the production
/// verb model stays the <c>Read</c>/<c>Write</c>/<c>Delete</c> record shape under
/// <c>app.types.path.permission.verb</c>. This enum exists only so the contract suite can
/// ask a fixture "does your backing system implement this verb at all?" when scoping which
/// contract tests run.
///
/// <c>CanPerform</c> is for SCOPING, never for skipping an assertion: a scheme whose server
/// returns 405 for a verb still "can perform" it in the contract sense — 405 is a return
/// value the suite asserts on. <c>CanPerform</c> answers only the genuine "this backend has
/// no such concept" case (e.g. an HTTP test server with no directory-listing route).
/// </summary>
public enum VerbName
{
    Read,
    Write,
    Append,
    Delete,
    Exists,
    Stat,
    List,
    CopyTo,
    MoveTo,
}
