namespace app.channel;

abstract class channel(string name) {
    public string name { get; } = name;
    public abstract Task write(object value);
}

class list(IReadOnlyList<channel> items) {
    public channel this[string name] => items.First(c => c.name == name);
}
