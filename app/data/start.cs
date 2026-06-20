namespace data;

// Data is the universal result + the universal carrier. Everything that flows is
// a Data. A courier relays it, reads Success/Error to gate, and never opens Value.
public class @this
{
    protected object? _value;

    public bool Success { get; protected set; } = true;
    public error.@this? Error { get; protected set; }

    public static @this Ok() => new();
    public static @this Ok(object? value) => new() { _value = value };
    public static @this Fail(error.@this error) => new() { Success = false, Error = error };
}

// Data<T> — the typed carrier. The leaf that declared T may open it with Value().
// A courier holding a Data<T> where T : IStart calls start(): Data forwards the
// verb to the value, so the courier never reads Value — it just delegates.
public sealed class @this<T> : @this
{
    public @this(T value) { _value = value; }

    // The leaf's door. Only a handler that declared Data<T> opens this.
    public Task<T> Value() => Task.FromResult((T)_value!);

    // The courier's door. Forward the verb; the value runs itself.
    public Task<data.@this> start() => ((plang.IStart)_value!).start();
}
