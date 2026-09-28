using app.@event;

namespace app.module.action.mock;

/// <summary>
/// Mocks a module (<c>file</c>) or one of its actions (<c>file.read</c>): a mock bound before it starts, for this
/// actor. It answers in the action's place with <see cref="Return"/> or the goal <see cref="Call"/> calls; with
/// neither it is a spy and only records. <see cref="Parameter"/> narrows it to calls whose parameters match.
/// Answers the mock — its own record of the calls.
/// </summary>
[Action("intercept", Cacheable = false)]
public partial class intercept : IContext
{
    /// <summary>What it mocks: a module name, or module.action.</summary>
    public partial data.@this<global::app.type.item.text.@this> Pattern { get; init; }
    public partial data.@this? Return { get; init; }
    /// <summary>The call run in place of the intercepted action — a <c>goal.call</c> action.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? Call { get; init; }
    public partial data.@this<global::app.type.item.dict.@this>? Parameter { get; init; }

    public async Task<data.@this<global::app.@event.binding.mock.@this>> Start()
    {
        var pattern = (await Pattern.Value())!.Clr<string>()!;
        var dot = pattern.IndexOf('.');
        var moduleName = dot < 0 ? pattern : pattern[..dot];
        var found = await Context.App.module.Get(moduleName);
        var module = found.Success ? await found.Value() as global::app.module.@this : null;
        global::app.type.item.@this? target = dot < 0 ? module : module?[pattern[(dot + 1)..]];
        if (target == null)
            return Context.Error<global::app.@event.binding.mock.@this>(new global::app.error.ActionError(
                $"There is no {(dot < 0 ? "module" : "action")} '{pattern}' to mock", "NotFound", 404));

        // A spy supplies neither a Return value nor a Call goal — it only observes. An unsupplied optional
        // param is a non-null Uninitialized Data (null model), so "was it supplied?" is IsInitialized.
        var returnValue = Return?.IsInitialized == true ? await Return.Value() : null;
        var call = Call?.IsInitialized == true ? await Call.Value() : null;
        var parameters = Parameter == null || await Parameter.IsEmpty() ? null
            : (await Parameter.Value()).Clr<Dictionary<string, object?>>();

        var mock = target.Own().Bind("start", When.before,
            side => new global::app.@event.binding.mock.@this(side, Context.Actor!, pattern, returnValue, call, parameters));
        return Context.Ok<global::app.@event.binding.mock.@this>(mock);
    }
}
