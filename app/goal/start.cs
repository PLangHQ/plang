// A goal is a named sequence of steps — the basic unit of a plang program. It is
// born by name (a reference) or by prPath (a located .pr), and either way holds
// the located file. Its steps are not handed in: they are read from the .pr at
// start(). Everything that flows is Data<T>.

namespace app.goal
{
    using text = app.type.text;
    using path = app.type.path;

    public sealed class @this : plang.IStart
    {
        public data.@this<text.@this>? name   { get; init; }   // born by name: a reference
        public data.@this<path.@this>? prPath { get; init; }   // born located: the .pr file

        // Run the goal: read its steps from the located .pr, then start them. The
        // file is found at birth, so this is just a read — never I/O in the ctor.
        public async Task<data.@this> start()
        {
            var step = await read();
            if (!step.Success) return step;
            return await step.start();   // Data<step.list> forwards start to the list
        }

        // prPath → step.list. Born by name, the goal resolves prPath first.
        // Deferred read — OBP smell #9 forbids I/O at construction.
        Task<data.@this<step.list.@this>> read() => throw new NotImplementedException();
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
