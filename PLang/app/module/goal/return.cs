namespace app.module.goal;

/// <summary>
/// Returns a value from the current goal. If the value is a failing Data, it propagates the error.
/// </summary>
[Action("return", Cacheable = false)]
public partial class Return : IContext
{
    public partial data.@this? Data { get; init; }

    [Default(1)]
    public partial data.@this<global::app.type.item.number.@this> Depth { get; init; }

    // The value returned, marked as the goal's return, leaving Depth goals (at least the current one); a depth
    // that didn't resolve is the answer. The number lowers itself at the engine's int return-depth slot.
    public Task<data.@this> Start() => Depth.Use(depth =>
    {
        var result = Data ?? Context.Ok();
        result.Returned = true;
        result.ReturnDepth = depth.ToInt32() is > 0 and var levels ? levels : 1;
        return Task.FromResult(result);
    });
}
