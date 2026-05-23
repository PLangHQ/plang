namespace app.types.path.permission.verb;

public sealed record Read(bool Recursive = true, bool Metadata = true)
{
    public bool Covers(Read request) =>
        (Recursive || !request.Recursive)
        && (Metadata || !request.Metadata);
}
