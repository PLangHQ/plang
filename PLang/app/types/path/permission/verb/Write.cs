namespace app.types.path.permission.verb;

public sealed record Write(bool Overwrite = true, bool Recursive = true)
{
    public bool Covers(Write request) =>
        (Overwrite || !request.Overwrite)
        && (Recursive || !request.Recursive);
}
