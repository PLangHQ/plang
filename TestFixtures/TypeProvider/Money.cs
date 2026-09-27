namespace TypeProvider;

/// <summary>
/// A minimal plang type that ships in a separate assembly. Loaded at runtime via
/// `- load TypeProvider.dll` — the type list adds <c>money</c> and registers the renderer below to
/// serve <c>(money, *)</c>.
/// </summary>
[global::app.Attributes.PlangType("money")]
public sealed class Money : global::app.type.item.@this
{
    public static string Example => "$10.00";
    public static string Shape => "string";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
}

public sealed class MoneyRenderer : global::app.type.list.ITypeRenderer
{
    public string TypeName => "money";
    public string Format => global::app.type.renderer.@this.AnyFormat;

    public void Write(object value, global::app.type.format.IWriter writer)
    {
        if (value is Money m)
            writer.String($"{m.Currency} {m.Amount}");
        else
            writer.Null();
    }
}
