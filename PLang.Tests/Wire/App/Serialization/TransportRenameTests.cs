namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 5
// Vocabulary sweep: "envelope" leaves the codebase. PLang/app/data/this.Envelope.cs is
// renamed to this.Transport.cs; the Signature docstring no longer reads "data envelope".

public class TransportRenameTests
{
    private static string RepoRoot()
    {
        var asmDir = System.IO.Path.GetDirectoryName(typeof(global::app.@this).Assembly.Location)!;
        var dir = asmDir;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang")))
            dir = System.IO.Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Repo root not found");
    }

    [Test] public async Task DataTransportFile_ExistsAtExpectedPath()
    {
        var path = System.IO.Path.Combine(RepoRoot(), "PLang", "app", "data", "this.Transport.cs");
        await Assert.That(System.IO.File.Exists(path)).IsTrue();
    }

    [Test] public async Task DataEnvelopeFile_NoLongerExistsAtOldPath()
    {
        var path = System.IO.Path.Combine(RepoRoot(), "PLang", "app", "data", "this.Envelope.cs");
        await Assert.That(System.IO.File.Exists(path)).IsFalse();
    }

    [Test] public async Task SignatureType_XmlDoc_NoLongerMentionsEnvelope()
    {
        // Read the type's XML doc from the source file — Roslyn doesn't bind <summary>
        // text at runtime, so the cheapest test is a substring check on the source.
        var path = System.IO.Path.Combine(RepoRoot(), "PLang", "app", "type", "signature", "this.cs");
        var content = await System.IO.File.ReadAllTextAsync(path);
        // Locate the class-level summary block (the first <summary> in the file).
        var summaryStart = content.IndexOf("/// <summary>", StringComparison.Ordinal);
        var summaryEnd = content.IndexOf("/// </summary>", summaryStart, StringComparison.Ordinal);
        await Assert.That(summaryStart >= 0 && summaryEnd > summaryStart).IsTrue();
        var summary = content.Substring(summaryStart, summaryEnd - summaryStart);
        await Assert.That(summary.Contains("envelope", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(summary).Contains("cryptographic-attestation");
    }
}
