namespace app.llm;

abstract class llm(string name) {
    public string name { get; } = name;
    public abstract Task<string> query(string prompt);
}

class list(IReadOnlyList<llm> items) {
    public llm default => items.First();
    public llm this[string name] => items.First(l => l.name == name);
}
