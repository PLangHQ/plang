namespace app.module.build;

/// <summary>
/// After a build: each private goal of the app that nothing reaches is written as a <c>GoalUnreached</c>
/// warning on the build's "builder" channel. Only the goals under the app's own root are asked, so an app's
/// build leaves <c>/system</c> out and a build of the os itself takes it in. A public goal is never warned:
/// any file can be run. Never a refusal.
/// </summary>
[Action("unreached", Cacheable = false)]
public partial class unreached : IContext
{
    public async Task<data.@this> Start()
    {
        var own = new global::app.type.item.dict.@this().Set("os", false);
        await foreach (var file in Context.App.goal.list.Walk(own, Context))
        {
            foreach (var (goal, from) in await file.Unreached(Context))
            {
                var message = from == null
                    ? $"'{goal.Name}' in {goal.Path} is not reached by any goal"
                    : $"'{goal.Name}' in {goal.Path} is reached only from '{from.Name}', which nothing reaches";
                await __action!.Warn(new global::app.error.Error(message, "GoalUnreached", 404), Context);
            }
        }
        return Context.Ok();
    }
}
