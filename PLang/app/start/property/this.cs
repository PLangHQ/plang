namespace app.start.property;

/// <summary>
/// One property the command line gives: its name as written, whether it is a flag (written <c>--x</c>: the app's, a
/// setting's path or a mode word) or a value (written <c>x=…</c>: the program's), and its value, read as JSON (text when
/// it isn't).
/// </summary>
public sealed class @this
{
    public string Name { get; }

    /// <summary>Written with <c>--</c>: the app's, never one of the program's variables.</summary>
    public bool IsFlag { get; }

    public object? Value { get; }

    internal @this(string name, bool isFlag, object? value)
    {
        Name = name;
        IsFlag = isFlag;
        Value = value;
    }
}
