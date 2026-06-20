namespace app.warning;

class warning(string message) {
    public string message { get; } = message;
}

class list(IReadOnlyList<warning> items) {
    readonly List<warning> _emitted = [];
    public void emit(warning w) => _emitted.Add(w);
    public IReadOnlyList<warning> all => _emitted;
}
