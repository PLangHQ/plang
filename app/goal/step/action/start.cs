// An action does one thing. Its inputs arrive as Data<T>; it returns Data, or
// Data<T> when the value is typed. The collection follows the @this convention:
// the element is `@this`, the collection is `list.@this` — so the `.list`
// property is legal (a class named `list` cannot have a `list` member).

namespace app.goal.step.action
{
    public abstract class @this
    {
        public abstract Task<data.@this> start();
    }
}

namespace app.goal.step.action.list
{
    using action = app.goal.step.action;

    // action.list — all the actions of a step. The list owns the loop and its
    // backing; reading its own backing to loop is owner behavior.
    public sealed class @this(data.@this<plang.list<action.@this>> actions) : plang.IStart
    {
        public data.@this<plang.list<action.@this>> list => actions;

        public async Task<data.@this> start()
        {
            data.@this result = data.@this.Ok();
            foreach (var a in await actions.Value())
            {
                result = await a.start();
                if (!result.Success) return result;
            }
            return result;
        }
    }
}
