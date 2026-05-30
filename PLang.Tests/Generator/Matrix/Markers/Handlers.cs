namespace app.module.matrix.markers;

[global::app.module.Action("icontexthandler")]
public partial class IContextHandler : global::app.module.IContext
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Context != null));
}

[global::app.module.Action("ichannelhandler")]
public partial class IChannelHandler : global::app.module.IContext, global::app.module.IChannel
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Channel != null));
}

[global::app.module.Action("iactionhandler")]
public partial class IActionHandler : global::app.module.IContext, global::app.module.IAction
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Action != null));
}

[global::app.module.Action("istephandler")]
public partial class IStepHandler : global::app.module.IContext, global::app.module.IStep
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Step != null));
}

[global::app.module.Action("istatichandler")]
public partial class IStaticHandler : global::app.module.IContext, global::app.module.IStatic
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Static != null));
}
