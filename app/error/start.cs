namespace app.error;

class error(string message, Exception? cause = null) {
    public string     message { get; } = message;
    public Exception? cause   { get; } = cause;
    public void start() => throw new RuntimeException(this);
}

class list(IReadOnlyList<error> items) {
    public void add(error e) => throw new RuntimeException(e);
}

class RuntimeException(error error) : Exception(error.message) {
    public error error { get; } = error;
}
