namespace App.FileSystem.Permission.Verb;

/// <summary>
/// Container for verb sub-records. Default ctor → all three verbs fully granted
/// (Read, Write, Delete with all sub-options true). Narrow by record-with:
/// <c>new @this { Write = new Write(Overwrite: false) }</c> or set to null to
/// deny the verb entirely.
/// </summary>
public sealed record @this
{
    public Read? Read { get; init; } = new Read();
    public Write? Write { get; init; } = new Write();
    public Delete? Delete { get; init; } = new Delete();

    public bool Covers(@this request)
    {
        if (request.Read is not null && (Read is null || !Read.Covers(request.Read))) return false;
        if (request.Write is not null && (Write is null || !Write.Covers(request.Write))) return false;
        if (request.Delete is not null && (Delete is null || !Delete.Covers(request.Delete))) return false;
        return true;
    }
}
