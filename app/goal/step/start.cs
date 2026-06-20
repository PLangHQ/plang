// A single step: one instruction. It runs its actions. Element is `@this`, the
// collection is `list.@this`.

namespace app.goal.step
{
    using text = app.type.text;
    using number = app.type.number;

    public sealed class @this(
        data.@this<text.@this> text,
        data.@this<number.@this> index,
        data.@this<action.list.@this> action)
    {
        public data.@this<text.@this>          text   { get; } = text;
        public data.@this<number.@this>        index  { get; } = index;
        public data.@this<action.list.@this>   action { get; } = action;

        // Run all the actions. step is a courier — it forwards. Data<action.list>
        // forwards start to its value; the list reads itself.
        public Task<data.@this> start() => action.start();
    }
}

namespace app.goal.step.list
{
    using step = app.goal.step;

    // step.list — all the steps of a goal. The list owns the loop and its backing.
    public sealed class @this(data.@this<plang.list<step.@this>> steps) : plang.IStart
    {
        public data.@this<plang.list<step.@this>> list => steps;

        public async Task<data.@this> start()
        {
            foreach (var s in await steps.Value())
            {
                var result = await s.start();
                if (!result.Success) return result;
            }
            return data.@this.Ok();
        }
    }
}
