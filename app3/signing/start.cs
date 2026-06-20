namespace app.signing;

abstract class signing {
    public abstract string sign(string data);
    public abstract bool   verify(string data, string signature);
}

class list(IReadOnlyList<signing> items) {
    public signing default => items.First();
}
