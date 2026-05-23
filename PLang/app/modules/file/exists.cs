using app.variables;
using app.types;

namespace app.modules.file;

/// <summary>
/// Resolves Path and returns it as Data&lt;Path&gt; — uniformly, for every
/// scheme. Existence itself is answered by the path: a comparison such as
/// <c>if %result% exists</c> routes <c>== true</c> through
/// <c>Data.ToBooleanAsync()</c> → <c>path.AsBooleanAsync()</c>, which probes
/// the filesystem (file scheme) or issues an HTTP HEAD (http scheme).
///
/// The action does no I/O of its own: the path stays live, so re-testing it
/// reflects the current state. (codeanalyzer v1 F3)
/// </summary>
[System.ComponentModel.Description("Check whether a file or directory exists at Path and return file info")]
[Example("check if file.txt exists, write to %fileInfo%",
    "file.exists Path([path] file.txt) | variable.set Name([variable] %fileInfo%), Value([path] %!data%)")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<path> Path { get; init; }

    // Returns Path directly — a failed scheme resolution (unregistered s3://)
    // is itself a non-Success Data, so the typed SchemeNotRegistered error
    // propagates instead of an NRE. (codeanalyzer v1 F4)
    //
    // Run returns Task<Data<path>> — method-signature-as-truth so the catalog
    // surfaces `→ returns path` and the compile LLM picks the right Type for
    // any trailing `variable.set` after a `write to %x%` (typed-returns work).
    public Task<data.@this<path>> Run() => Task.FromResult(Path);
}
