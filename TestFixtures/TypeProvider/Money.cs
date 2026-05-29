namespace TypeProvider;

/// <summary>
/// A minimal [PlangType] that ships in a separate assembly. Loaded at
/// runtime via `- load TypeProvider.dll` — the Stage 7 Loader scans the
/// assembly, registers <c>money</c> in the registry, and instantiates the
/// renderer below to serve <c>(money, *)</c>.
/// </summary>
[global::app.Attributes.PlangType("money")]
public sealed class Money
{
    public static string Example => "$10.00";
    public static string Shape => "string";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
}

public sealed class MoneyRenderer : global::app.types.ITypeRenderer
{
    public string TypeName => "money";
    public string Format => global::app.types.renderers.@this.AnyFormat;

    public void Write(object value, global::app.channels.serializers.IWriter writer)
    {
        if (value is Money m)
            writer.String($"{m.Currency} {m.Amount}");
        else
            writer.Null();
    }
}

/// <summary>
/// Overrides the built-in <c>int</c> with a custom CLR type + renderer to
/// exercise the runtime-wins precedence at the registry + dispatch table.
/// </summary>
[global::app.Attributes.PlangType("int")]
public sealed class CustomInt
{
    public static string Example => "0";
    public static string Shape => "string";
}

public sealed class CustomIntRenderer : global::app.types.ITypeRenderer
{
    public string TypeName => "int";
    public string Format => global::app.types.renderers.@this.AnyFormat;
    public void Write(object value, global::app.channels.serializers.IWriter writer)
        => writer.String("CUSTOM-INT");
}
