// A goal is a named sequence of steps — the basic unit of a plang program.
// Everything it holds flows as Data<T>, the step.list included.

namespace app.goal
{
    using text = app.type.text;
    using path = app.type.path;

    public sealed class @this(
        data.@this<text.@this> name,
        data.@this<path.@this> path,
        data.@this<step.list.@this> step)
    {
        public data.@this<text.@this>        name { get; } = name;
        public data.@this<path.@this>        path { get; } = path;
        public data.@this<step.list.@this>   step { get; } = step;

        // Run all the steps. goal is a courier — it forwards to the Data, which
        // forwards start to the step.list. The goal never opens Value.
        public Task<data.@this> start() => step.start();
    }
}

namespace app.goal.list
{
    using goal = app.goal;

    // goal.list — all the goals of a program. The list owns the loop and backing.
    public sealed class @this(data.@this<plang.list<goal.@this>> goals) : plang.IStart
    {
        public data.@this<plang.list<goal.@this>> list => goals;

        public async Task<data.@this> start()
        {
            foreach (var g in await goals.Value())
            {
                var result = await g.start();
                if (!result.Success) return result;
            }
            return data.@this.Ok();
        }
    }
}
