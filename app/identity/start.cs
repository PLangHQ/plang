namespace app.identity;

class identity(string name, string publicKey) {
    public string name      { get; } = name;
    public string publicKey { get; } = publicKey;
}

class list(IReadOnlyList<identity> items) {
    public identity? current { get; private set; }
    public void set(string name) => current = items.First(i => i.name == name);
}
