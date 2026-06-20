using app.type.text;
using app.type.number;

namespace app.goal.step;

// A single step: one instruction. It runs its actions.
// Everything it holds flows as Data<T> — the action.list included.
class step(data.@this<text.@this> text, data.@this<number.@this> index, data.@this<action.list> action) {
    public data.@this<text.@this>   text   { get; } = text;
    public data.@this<number.@this> index  { get; } = index;
    public data.@this<action.list>  action { get; } = action;

    // Run all the actions — run the list.
    public Task<data.@this> start() => action.list.start();
}

// step.list — all the steps of a goal. The list owns the loop and its backing.
class list(data.@this<plang.list<step>> steps) {
    public data.@this<plang.list<step>> list => steps;

    public async Task<data.@this> start() {
        foreach (var s in await steps.Value()) {
            var result = await s.start();
            if (!result.Success) return result;
        }
        return data.@this.Ok();
    }
}
