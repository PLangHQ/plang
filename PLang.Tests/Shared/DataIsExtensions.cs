namespace PLang.Tests;

/// <summary>Test reads of a Data's type by name.</summary>
public static class DataIsExtensions
{
    /// <summary>Is this value (now or in its narrow history) the type <paramref name="typeName"/>
    /// names, through the Data's own app's types.</summary>
    public static bool Is(this global::app.data.@this data, string typeName)
        => data.Context is { } context && data.Is(context.App.type.list[typeName]);
}
