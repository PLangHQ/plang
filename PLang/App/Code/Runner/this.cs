namespace App.Code.Runner;

/// <summary>
/// Path-aware facade: <c>new Runner.@this(path).Start(data)</c> loads the file
/// at <c>path</c> and dispatches <c>Start(data)</c> on the resulting runtime.
/// One responsibility — turn a path into a result. Path stays in its envelope;
/// unwrap only happens at the work site (the file read inside Code.Load).
/// </summary>
public sealed class @this
{
    private readonly Data.@this<FileSystem.Path> _path;

    public @this(Data.@this<FileSystem.Path> path) { _path = path; }

    public async Task<Data.@this> Start(Data.@this data)
    {
        var ctx = _path.Value!.Context!;
        var loaded = await ctx.App.Code.Load(_path);
        if (!loaded.Success) return loaded;

        await using var runtime = loaded.Value!;
        return await runtime.Start(data, ctx);
    }
}
