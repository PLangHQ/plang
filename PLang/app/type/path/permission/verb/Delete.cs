namespace app.type.path.permission.verb;

public sealed record Delete(bool Recursive = true)
{
    public bool Covers(Delete request) =>
        Recursive || !request.Recursive;
}
