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
        switch (value.Kind)
        {
            case global::app.type.number.NumberKind.Int:
                writer.Int(value.ToInt32()); return;
            case global::app.type.number.NumberKind.Long:
                writer.Long(value.ToInt64()); return;
            case global::app.type.number.NumberKind.Decimal:
                writer.Decimal(value.ToDecimal()); return;
            case global::app.type.number.NumberKind.Float:
                writer.Float(value.ToSingle()); return;
            case global::app.type.number.NumberKind.Double:
                writer.Double(value.ToDouble()); return;
            default:
                throw new global::System.InvalidOperationException(
                    $"number.serializer.Default: unknown NumberKind {value.Kind}");
        }
    }
}
