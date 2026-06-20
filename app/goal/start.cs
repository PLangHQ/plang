using app.type.text;
using app.type.path;

namespace app.goal;

// A goal is a named sequence of steps — the basic unit of a plang program.
// Everything it holds flows as Data<T>, collections included: a bare step.list
// would mean someone cracked open a Data to hand it over. We never decompose.
class goal(data.@this<text.@this> name, data.@this<path.@this> path, data.@this<step.list> step) {
    public data.@this<text.@this> name { get; } = name;
    public data.@this<path.@this> path { get; } = path;
    public data.@this<step.list>  step { get; } = step;

    // Run all the steps — run the list.
    public Task<data.@this> start() => step.list.start();
}

// goal.list — all the goals of a program. The list owns the loop.
class list(plang.list<goal> goals) {
    public plang.list<goal> list => goals;

    public async Task<data.@this> start() {
        foreach (var g in goals) {
            var result = await g.start();
            if (!result.Success) return result;
        }
        return data.@this.Ok();
    }
}
