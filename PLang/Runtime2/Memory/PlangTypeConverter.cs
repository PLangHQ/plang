using System.ComponentModel;
using System.Globalization;

namespace PLang.Runtime2.Memory;

/// <summary>
/// Standard TypeConverter for <see cref="Type"/>.
/// Newtonsoft.Json automatically discovers this via the [TypeConverter] attribute,
/// so Runtime2 needs no Newtonsoft dependency.
/// </summary>
public sealed class PlangTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, System.Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
            return new Type(s);

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, System.Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, System.Type destinationType)
    {
        if (destinationType == typeof(string) && value is Type t)
            return t.Value;

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
