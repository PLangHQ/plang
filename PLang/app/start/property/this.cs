namespace app.start.property;

/// <summary>
/// One property the command line gives: its name as the start keeps it — a flag written <c>--x</c> is <c>!x</c>, a
/// property written <c>x=…</c> is <c>x</c> — and its value, read as JSON (text when it isn't).
/// </summary>
public sealed class @this
{
    public string Name { get; }

    public object? Value { get; }

    internal @this(string name, object? value)
    {
        Name = name;
        Value = value;
    }
}
