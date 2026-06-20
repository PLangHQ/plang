namespace app.goal.step.action;

// Every PLang action is an action. Its data arrives as Data<T> — typed, lazy,
// signed. It starts, does one thing, and returns data.
abstract class action {
    public abstract Task<data.@this> start();
}

class list(list<action> actions) {
    public async Task<data.@this> start() {
        data.@this result = data.@this.Ok();
        foreach (var a in actions)
        {
            result = await a.start();
            if (!result.Success) return result;
        }
        return result;
    }
}
