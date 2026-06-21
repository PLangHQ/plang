using app.type.text;
using app.type.number;

namespace app.goal.step;

class step(data.@this<text.@this> text, data.@this<number.@this> index, action.list actions) {
    public data.@this<text.@this>   text    { get; } = text;
    public data.@this<number.@this> index   { get; } = index;
    public action.list              actions { get; } = actions;

    public async Task<data.@this> start() => await actions.start();
}

class list(list<step> steps) {
    public async Task<data.@this> start() {
        foreach (var s in steps)
        {
            var result = await s.start();
            if (!result.Success) return result;
        }
        return data.@this.Ok();
    }
}
