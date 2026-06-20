namespace app.type.number;

// number — a plang numeric value. Never a raw `int`/`double`; the real one
// carries its precision as a kind (int, long, decimal, double).
public sealed class @this(double value)
{
    public double Value { get; } = value;
}
