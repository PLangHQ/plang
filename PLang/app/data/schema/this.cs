namespace app.data.schema;

/// <summary>
/// The <c>@schema</c> reader registry — the Data-level mirror of
/// <see cref="app.type.reader.@this"/> (which keys value readers by <c>(type, kind)</c>). The
/// writer emits <c>@schema</c> first; the Wire reads it and dispatches here:
/// <see cref="Reader"/> returns the <see cref="ISchemaReader"/> for that schema (<c>data</c>
/// reads the envelope, <c>signature</c> verifies + peels). The readers are stateless (per-read
/// state rides <c>ReadContext</c>), so one shared instance serves every read.
/// </summary>
public sealed class @this
{
    public static readonly @this Instance = new();

    private readonly System.Collections.Generic.Dictionary<string, ISchemaReader> _readers;

    private @this()
    {
        _readers = new(System.StringComparer.OrdinalIgnoreCase);
        Add(new global::app.data.reader.@this());
        Add(new signature());
    }

    private void Add(ISchemaReader reader) => _readers[reader.Schema] = reader;

    /// <summary>The reader for <paramref name="schema"/>, or throws LOUDLY for an unknown
    /// layer — a wire @schema value with no reader is a malformed/forward-version payload.</summary>
    public ISchemaReader Reader(string schema)
        => _readers.TryGetValue(schema, out var reader) ? reader
           : throw new System.NotSupportedException(
               $"no reader for @schema '{schema}' — a Data wire layer must be a registered ISchemaReader.");
}
