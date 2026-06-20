namespace app.goal.step.action;

// An action does one thing. Its inputs arrive as Data<T> — typed, lazy, signed.
// A concrete action that needs the execution context implements IContext; it is
// never passed in. The return is data.@this, or data.@this<T> when the action
// produces a typed value (a db read returns Data<table>, math.add returns
// Data<number>); bare data.@this only when it produces no value.
abstract class action {
    public abstract Task<data.@this> start();
}

// action.list — all the actions of a step. The list owns the loop and its backing.
class list(data.@this<plang.list<action>> actions) {
    public data.@this<plang.list<action>> list => actions;

    public async Task<data.@this> start() {
        data.@this result = data.@this.Ok();
        foreach (var a in await actions.Value()) {
            result = await a.start();
            if (!result.Success) return result;
        }
        return result;
    }
}
