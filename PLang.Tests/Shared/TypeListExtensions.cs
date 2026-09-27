namespace PLang.Tests;

/// <summary>Test reads over the app's type list.</summary>
public static class TypeListExtensions
{
    /// <summary>The C# class of the type <paramref name="name"/> names, or null when it names none.</summary>
    public static System.Type? Clr(this global::app.type.list.@this types, string name)
        => types.Contains(name) ? types[name].ClrType : null;
}
