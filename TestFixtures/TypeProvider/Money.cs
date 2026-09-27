namespace TypeProvider;

/// <summary>
/// A minimal plang type that ships in a separate assembly. Loaded at runtime via
/// `- load TypeProvider.dll` — the type list adds <c>money</c>. A money value writes
/// itself as <c>"{Currency} {Amount}"</c> in every format.
/// </summary>
[global::app.Attributes.PlangType("money")]
public sealed class Money : global::app.type.item.@this
{
    public static string Example => "$10.00";
    public static string Shape => "string";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }

    public override void Write(global::app.type.format.IWriter writer) => writer.String($"{Currency} {Amount}");
}
