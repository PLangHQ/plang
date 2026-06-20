using app.type.text;
using app.type.path;

namespace app.goal;

class goal(data.@this<text.@this> name, data.@this<path.@this> path, step.list steps) {
    public data.@this<text.@this> name  { get; } = name;
    public data.@this<path.@this> path  { get; } = path;
    public step.list               steps { get; } = steps;

    public async Task<data.@this> start() => await steps.start();
}

class list(list<goal> goals) {
    public async Task<data.@this> start() {
        foreach (var g in goals)
        {
            var result = await g.start();
            if (!result.Success) return result;
        }
        return data.@this.Ok();
    }
}
