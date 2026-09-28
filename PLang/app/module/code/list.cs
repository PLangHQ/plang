namespace app.module.code;

/// <summary>
/// Lists registered providers, optionally of one kind.
/// PLang: list signing providers
/// </summary>
[Action("list", Cacheable = false)]
public partial class list : IContext
{
    /// <summary>The kind of provider to list. Omit to list all.</summary>
    public partial data.@this<global::app.type.item.choice.@this<kind.@this>>? Type { get; init; }

    // Providers are plumbing — PLang sees their names (list<text>), not the CLR instances.
    public Task<data.@this> Start() => Type == null
        ? Task.FromResult(Data(Context.App.Code.List().Select(p => p.Name).ToList()))
        : Type.Use(kind => Task.FromResult(Data(Context.App.Code.List(kind.Value.Interface).Select(p => p.Name).ToList())));
}
