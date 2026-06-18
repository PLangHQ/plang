namespace app.type.number.serializer;

using TokenKind = global::app.channel.serializer.TokenKind;
using NumberKind = global::app.type.number.NumberKind;
using num = global::app.type.number.@this;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.number.@this"/> — the inverse of <see cref="Default.Write"/>.
/// <paramref name="kind"/> names the exact precision (it always rides the wire for
/// a number — the type entity carries the <see cref="NumberKind"/>), so the reader
/// pulls the matching token and borns the number at that kind directly via
/// <c>From</c> — no re-coercion, no <c>ChangeType</c>. Kinds beyond the JSON numeric
/// token (Int128/UInt128/BigInteger, and an overflowing ULong) ride as a string,
/// exactly as the writer emits them.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("number", kind);

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        switch (num.KindFromName(kind))
        {
            case NumberKind.SByte: return num.From((sbyte)reader.Int());
            case NumberKind.Byte: return num.From((byte)reader.Int());
            case NumberKind.Short: return num.From((short)reader.Int());
            case NumberKind.UShort: return num.From((ushort)reader.Int());
            case NumberKind.Int: return num.From(reader.Int());
            case NumberKind.UInt: return num.From((uint)reader.Long());
            case NumberKind.Long: return num.From(reader.Long());
            case NumberKind.ULong:
                return num.From(reader.Peek() == TokenKind.String
                    ? ulong.Parse(reader.String(), inv)
                    : (ulong)reader.Long());
            case NumberKind.Float: return num.From(reader.Float());
            case NumberKind.Half: return num.From((System.Half)reader.Double());
            case NumberKind.Double: return num.From(reader.Double());
            case NumberKind.Decimal: return num.From(reader.Decimal());
            case NumberKind.Int128: return num.From(System.Int128.Parse(reader.String(), inv));
            case NumberKind.UInt128: return num.From(System.UInt128.Parse(reader.String(), inv));
            case NumberKind.BigInteger: return num.From(System.Numerics.BigInteger.Parse(reader.String(), inv));
            default:
                // No declared kind — not emitted by our writer (a number always
                // stamps its NumberKind). Defensive: a bare numeric token reads at
                // its natural precision, a string parses through the family.
                return reader.Peek() == TokenKind.String
                    ? (num.FromObject(reader.String()) ?? (global::app.type.item.@this)
                        new global::app.type.@null.@this("number", kind))
                    : num.From(reader.Double());
        }
    }
}
