namespace app.type.path;

// path — a plang filesystem path value. Never a raw `string`; the real one
// gates every verb through AuthGate and resolves at the perimeter.
public sealed class @this(string value)
{
    public string Value { get; } = value;
    public override string ToString() => Value;
}
