namespace app.type.number;

/// <summary>
/// <see cref="System.IConvertible"/> bridge — lets <see cref="System.Convert.ToDouble"/>,
/// <see cref="System.Convert.ToInt32"/>, etc. accept a <see cref="@this"/>
/// transparently. Without this, code paths that read <c>Data.Value</c> (now
/// a <see cref="@this"/> after the Stage 4 math.* retype) and pipe it
/// through <c>Convert.*</c> throw <see cref="System.InvalidCastException"/>.
/// </summary>
public sealed partial class @this
{
    public System.TypeCode GetTypeCode() => Kind.Name switch
    {
        "int" => System.TypeCode.Int32,
        "long" => System.TypeCode.Int64,
        "decimal" => System.TypeCode.Decimal,
        "double" => System.TypeCode.Double,
        "float" => System.TypeCode.Single,
        _ => System.TypeCode.Object,
    };

    public bool ToBoolean(System.IFormatProvider? p) => AsBooleanAsync().GetAwaiter().GetResult();
    public byte ToByte(System.IFormatProvider? p) => checked((byte)ToInt32());
    public sbyte ToSByte(System.IFormatProvider? p) => checked((sbyte)ToInt32());
    public short ToInt16(System.IFormatProvider? p) => checked((short)ToInt32());
    public ushort ToUInt16(System.IFormatProvider? p) => checked((ushort)ToInt32());
    public int ToInt32(System.IFormatProvider? p) => ToInt32();
    public uint ToUInt32(System.IFormatProvider? p) => checked((uint)ToInt64());
    public long ToInt64(System.IFormatProvider? p) => ToInt64();
    public ulong ToUInt64(System.IFormatProvider? p) => checked((ulong)ToInt64());
    public float ToSingle(System.IFormatProvider? p) => ToSingle();
    public double ToDouble(System.IFormatProvider? p) => ToDouble();
    public decimal ToDecimal(System.IFormatProvider? p) => ToDecimal();
    public string ToString(System.IFormatProvider? p) => ToString();
    public char ToChar(System.IFormatProvider? p) => throw new System.InvalidCastException("number cannot be converted to char");
    public System.DateTime ToDateTime(System.IFormatProvider? p) => throw new System.InvalidCastException("number cannot be converted to DateTime");

    public object ToType(System.Type conversionType, System.IFormatProvider? p)
    {
        if (conversionType == typeof(int)) return ToInt32();
        if (conversionType == typeof(long)) return ToInt64();
        if (conversionType == typeof(decimal)) return ToDecimal();
        if (conversionType == typeof(double)) return ToDouble();
        if (conversionType == typeof(float)) return ToSingle();
        if (conversionType == typeof(string)) return ToString();
        if (conversionType == typeof(bool)) return AsBooleanAsync().GetAwaiter().GetResult();
        if (conversionType == typeof(object)) return this;
        throw new System.InvalidCastException($"number cannot be converted to {conversionType}");
    }
}
