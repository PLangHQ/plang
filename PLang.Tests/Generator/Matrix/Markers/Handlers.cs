namespace app.modules.matrix.markers;

[global::app.modules.Action("icontexthandler")]
public partial class IContextHandler : global::app.modules.IContext
{
    public Task<global::app.Data.@this> Run() =>
        Task.FromResult(global::app.Data.@this.Ok(Context != null));
}

[global::app.modules.Action("ichannelhandler")]
public partial class IChannelHandler : global::app.modules.IContext, global::app.modules.IChannel
{
    public Task<global::app.Data.@this> Run() =>
        Task.FromResult(global::app.Data.@this.Ok(Channel != null));
}

[global::app.modules.Action("iactionhandler")]
public partial class IActionHandler : global::app.modules.IContext, global::app.modules.IAction
{
    public Task<global::app.Data.@this> Run() =>
        Task.FromResult(global::app.Data.@this.Ok(Action != null));
}

[global::app.modules.Action("istephandler")]
public partial class IStepHandler : global::app.modules.IContext, global::app.modules.IStep
{
    public Task<global::app.Data.@this> Run() =>
        Task.FromResult(global::app.Data.@this.Ok(Step != null));
}

[global::app.modules.Action("istatichandler")]
public partial class IStaticHandler : global::app.modules.IContext, global::app.modules.IStatic
{
    public Task<global::app.Data.@this> Run() =>
        Task.FromResult(global::app.Data.@this.Ok(Static != null));
}
