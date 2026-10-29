using System.Security.Cryptography;
using System.Text;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// The answers the llm integration tests replay, each keyed on the request body sent — the rule plang tests' llm
/// answers follow: the same request is the same question, so an answer misses only when what is sent changes.
/// An answer is read from the ones kept in the repo, then from this machine's own; a new one is written to this
/// machine's own (a temp folder), never into the repo — a test run leaves the tree as it found it.
/// </summary>
internal static class LlmSnapshotHelper
{
    private static readonly string Kept = System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..",
        "App", "Modules", "llm", "snapshots");

    private static readonly string Recorded = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_llm_snapshots");

    // the answer's file: the repo's when it keeps one, else this machine's
    private static string Snapshot(string file)
    {
        var kept = System.IO.Path.Combine(Kept, file);
        return System.IO.File.Exists(kept) ? kept : System.IO.Path.Combine(Recorded, file);
    }

    /// <summary>The request body's key: the name its answer is kept under.</summary>
    internal static string Key(string request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request))).ToLowerInvariant()[..32];

    /// <summary>The answer recorded for this request body, or null.</summary>
    internal static string? Answer(string request)
    {
        var path = Snapshot($"{Key(request)}.json");
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
    }

    /// <summary>Records the answer to this request body on this machine.</summary>
    internal static void Save(string request, string response)
    {
        System.IO.Directory.CreateDirectory(Recorded);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Recorded, $"{Key(request)}.json"), response);
    }
}
