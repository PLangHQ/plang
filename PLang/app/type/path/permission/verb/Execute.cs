namespace app.type.path.permission.verb;

/// <summary>
/// Execute permission — distinct from Read, mirroring the Unix r/w/x model.
/// Granted when a path is to be loaded as code (DLL via
/// <see cref="@this.LoadAssemblyAsync"/>, scripts handed to an interpreter,
/// etc). Carries no sub-options today; reserved for future extension
/// (e.g. signature-required, isolated-context).
/// </summary>
public sealed record Execute
{
    public bool Covers(Execute request) => true;
}
