namespace app.type.number.serializer;

/// <summary>
/// Wire renderer for <see cref="app.type.number.@this"/> — emits the
/// matching <see cref="app.channel.serializer.IWriter"/> numeric primitive
/// by <see cref="app.type.number.NumberKind"/>. Uniform across formats:
/// the <c>IWriter</c> primitive vocabulary IS the cross-format contract for
/// numeric values (every format encoder knows what an Int is). One file,
/// one decision; no per-format variants needed.
/// </summary>
public static class Default
{
    public static void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        // IWriter's numeric vocabulary covers int/long/float/double/decimal as
        // native tokens; the precise kind rides the type envelope (Way 3), so a
        // narrower/wider integer can serialize through the widest native token
        // it fits. Kinds beyond IWriter's vocabulary (Int128/UInt128/BigInteger)
        // serialize as their lossless invariant string.
        switch (value.Kind)
        {
            case global::app.type.number.NumberKind.SByte:
            case global::app.type.number.NumberKind.Byte:
            case global::app.type.number.NumberKind.Short:
            case global::app.type.number.NumberKind.UShort:
            case global::app.type.number.NumberKind.Int:
                writer.Int(value.ToInt32()); return;
            case global::app.type.number.NumberKind.UInt:
            case global::app.type.number.NumberKind.Long:
                writer.Long(value.ToInt64()); return;
            case global::app.type.number.NumberKind.ULong:
            {
                ulong ul = (ulong)value.BoxedValue;
                if (ul <= long.MaxValue) writer.Long((long)ul);
                else writer.String(ul.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            case global::app.type.number.NumberKind.Float:
                writer.Float(value.ToSingle()); return;
            case global::app.type.number.NumberKind.Half:
            case global::app.type.number.NumberKind.Double:
                writer.Double(value.ToDouble()); return;
            case global::app.type.number.NumberKind.Decimal:
                writer.Decimal(value.ToDecimal()); return;
            case global::app.type.number.NumberKind.Int128:
            case global::app.type.number.NumberKind.UInt128:
            case global::app.type.number.NumberKind.BigInteger:
                writer.String(value.ToString()); return;
            default:
                throw new global::System.InvalidOperationException(
                    $"number.serializer.Default: unhandled NumberKind {value.Kind}");
        }
    }

    /// <summary>
    /// Read mirror of <see cref="Write"/> — re-houses the per-family
    /// <c>number.Convert</c> hook behind the reader registry. The decode logic
    /// is not rewritten: <paramref name="kind"/> picks the CLR precision and the
    /// raw value parses invariant-culture, exactly as the eager convert path did.
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.number.@this.Convert(raw, kind, ctx.Context!).Value;
}
