using app.type.path;

namespace app.file;

class file(data.@this<path.@this> path) {
    public data.@this<path.@this> path { get; } = path;

    public bool is(data.@this<path.@this> target) => path.Equals(target);

    public async Task<data.@this> start() {
        var goal = await load();
        if (!goal.Success) return goal;
        return await (await goal.Value())!.start();
    }

    async Task<data.@this<goal.goal>> load() =>
        throw new NotImplementedException();

    public class list(list<file> files) {
        public file? find(data.@this<path.@this> path) =>
            files.first(f => f.is(path));
    }
}
