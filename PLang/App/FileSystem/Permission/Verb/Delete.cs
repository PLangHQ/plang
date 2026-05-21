namespace App.FileSystem.Permission.Verb;

public sealed record Delete(bool Recursive = true)
{
    public bool Covers(Delete request) =>
        Recursive || !request.Recursive;
}
