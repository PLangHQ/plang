namespace app.type.item;

/// <summary>
/// The one of its kind a location holds — a goal's <c>.pr</c> (later a module's DLL). The answer is the Data a
/// load produced, as is: the one read, or what a binding before the load answered in its place. A concept that
/// isn't loaded from a location answers so — the default.
/// </summary>
public interface ILoad<TSelf> where TSelf : @this, ILoad<TSelf>
{
    static virtual System.Threading.Tasks.Task<global::app.data.@this> Load(path.@this location, global::app.@this app)
        => System.Threading.Tasks.Task.FromResult(app.System.Context.Error(new global::app.error.Error(
            $"a {@this.NameOf(typeof(TSelf))} isn't loaded from a location", "NotSupported", 400)));
}
