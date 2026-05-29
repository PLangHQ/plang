namespace app.type.path.permission.verb;

/// <summary>
/// Container for verb sub-records.
///
/// <b>Default ctor</b> (<c>new @this()</c>) → all three verbs null. Set the
/// specific verb(s) you want: <c>new @this { Read = new Read() }</c>. This
/// is what JSON survives (<c>JsonIgnoreCondition.WhenWritingNull</c> on the
/// Plang serializer means non-set verbs are omitted from the wire and stay
/// null on deserialize — signature bytes survive sqlite round-trip).
///
/// For "fully granted" grants, use <see cref="AllowAll"/> which sets all
/// three sub-verbs at their default-true options.
/// </summary>
public sealed record @this
{
    public Read? Read { get; init; }
    public Write? Write { get; init; }
    public Delete? Delete { get; init; }
    public Execute? Execute { get; init; }

    /// <summary>
    /// "Fully granted" — all four verbs at their default-true options. Use
    /// when the grant covers everything for a path (e.g. test fixtures,
    /// blanket grants). Requests narrow naturally by setting one verb.
    /// Execute is opt-in — Read grants do NOT cover Execute (Unix model:
    /// reading a DLL is not permission to load it).
    /// </summary>
    public static @this AllowAll() =>
        new() { Read = new Read(), Write = new Write(), Delete = new Delete(), Execute = new Execute() };

    public bool Covers(@this request)
    {
        if (request.Read is not null && (Read is null || !Read.Covers(request.Read))) return false;
        if (request.Write is not null && (Write is null || !Write.Covers(request.Write))) return false;
        if (request.Delete is not null && (Delete is null || !Delete.Covers(request.Delete))) return false;
        if (request.Execute is not null && (Execute is null || !Execute.Covers(request.Execute))) return false;
        return true;
    }
}
