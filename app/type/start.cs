namespace app.type;

abstract class type(string name) {
    public string name { get; } = name;
    public abstract void start();
}

class list(IReadOnlyList<type> items) {
    public type this[string name] => items.First(t => t.name == name);
}
