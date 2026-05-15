using System.ComponentModel;
using System.Globalization;

namespace app.data;

/// <summary>
/// Standard TypeConverter for <see cref="Type"/>.
/// Newtonsoft.Json automatically discovers this via the [TypeConverter] attribute,
/// so App needs no Newtonsoft dependency.
/// </summary>
public sealed class Converter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, System.Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
            return new type(s);

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, System.Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, System.Type destinationType)
    {
        if (destinationType == typeof(string) && value is type t)
            return t.Value;

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
