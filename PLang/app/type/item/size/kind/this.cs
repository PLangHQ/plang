namespace app.type.item.size.kind;

/// <summary>
/// A standard a size is written in — a kind of <c>size</c> (<c>iec</c> 95.4 MiB, <c>si</c> 100 MB). Each owns both
/// directions of its form: the bytes a text in it is, and a count's text in it. The suffixes don't overlap, so a text
/// names its own kind; a unit is never guessed.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    // the units above a byte, smallest first, and how many bytes one of the smallest is
    private readonly string[] _units;
    private readonly long _step;

    protected @this(string name, long step, params string[] units) : base(name)
    {
        _step = step;
        _units = units;
    }

    protected internal override string Owner => "size";

    /// <summary>The bytes <paramref name="text"/> is in this standard — a number and one of its units; null when it
    /// isn't written in it.</summary>
    internal long? Bytes(string text)
    {
        var form = System.Text.RegularExpressions.Regex.Match(text, @"^(\d+(?:\.\d+)?)\s*([a-z]+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!form.Success) return null;
        var unit = System.Array.FindIndex(_units, u => string.Equals(u, form.Groups[2].Value, System.StringComparison.OrdinalIgnoreCase));
        if (unit < 0) return null;
        var amount = decimal.Parse(form.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i <= unit; i++) amount *= _step;
        return (long)decimal.Round(amount);
    }

    /// <summary><paramref name="bytes"/> written in this standard: its largest unit that is at least one, with one
    /// decimal at most (<c>95.4 MiB</c>, <c>100 MB</c>); under the smallest unit, bytes (<c>512 B</c>).</summary>
    internal string Text(long bytes)
    {
        decimal amount = bytes;
        var unit = -1;
        while (unit + 1 < _units.Length && System.Math.Abs(amount) >= _step)
        {
            amount /= _step;
            unit++;
        }
        var number = decimal.Round(amount, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return $"{number} {(unit < 0 ? "B" : _units[unit])}";
    }

    /// <summary>A text in this standard, as a size born with it.</summary>
    public override global::app.type.item.@this? Parse(object raw, global::app.actor.context.@this ctx)
        => raw is string text && Bytes(text.Trim()) is { } bytes ? new size.@this(bytes, this) : null;
}
