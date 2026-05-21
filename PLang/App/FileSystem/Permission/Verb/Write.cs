namespace App.FileSystem.Permission.Verb;

public sealed record Write(bool Overwrite = true, bool Recursive = true)
{
    public bool Covers(Write request) =>
        (Overwrite || !request.Overwrite)
        && (Recursive || !request.Recursive);
}
