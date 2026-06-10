namespace PLang.Tests;

/// <summary>
/// Parse-state probe for source-backed Data. The old <c>Data.MaterializeCount()</c>
/// instrumentation died with <c>Materialize()</c> — parse now lives inside the
/// instance's own door and rebinds the Data. The observable contract the lazy
/// tests pin is binary: has the source form parsed (rebound) or not. 0 = still
/// source-backed (untouched), 1 = parsed/authored. Parse-once is implied by the
/// rebind: once rebound there is no source left to re-parse.
/// </summary>
public static class MaterializeProbeExtensions
{
    public static int MaterializeCount(this global::app.data.@this d)
    {
        if (d.RawUntouched) return 0;
        // Parsed values carry their unparsed form in the prior chain (the
        // source / the file the parse rebound away from); authored values have
        // no such prior and never parsed.
        for (var p = d.Instance?.Prior; p != null; p = p.Prior)
            if (p is global::app.type.item.source
                or global::app.type.file.@this
                or global::app.type.url.@this)
                return 1;
        return 0;
    }
}
