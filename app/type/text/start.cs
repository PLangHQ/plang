namespace app.type.text;

// text — a plang string value. Carries the value so signatures read in plang
// types, never raw `string`. The real one grows truthiness, navigation, kinds.
public sealed class @this(string value)
{
    public string Value { get; } = value;
    public override string ToString() => Value;
}
