namespace app.module.code.kind;

/// <summary>
/// A kind of provider — <c>signing</c>, <c>key</c>, <c>llm</c>, … — carrying the interface its providers
/// serve. The kinds are the provider interfaces the runtime declares, each named by its interface
/// (<c>ISigning</c> is <c>signing</c>); an unknown name is the choice's own error.
/// </summary>
[global::app.Attributes.PlangType("provider"), global::app.Attributes.Choice]
public sealed class @this
{
    private static readonly System.Type[] Interfaces = typeof(ICode).Assembly.GetTypes()
        .Where(t => t.IsInterface && t != typeof(ICode) && typeof(ICode).IsAssignableFrom(t))
        .ToArray();

    [global::app.Attributes.Choices]
    public static string[] Choices(actor.context.@this? context) => [.. Interfaces.Select(Named).Order(System.StringComparer.Ordinal)];

    /// <summary>The interface this kind's providers serve — the registry's key.</summary>
    public System.Type Interface { get; }

    [Out] public string Value { get; }

    public @this(string value)
    {
        Interface = Interfaces.FirstOrDefault(i => string.Equals(Named(i), value, System.StringComparison.OrdinalIgnoreCase))
            ?? throw new System.ArgumentException(
                $"Unknown provider kind '{value}'. Valid: {string.Join(", ", Choices(null))}");
        Value = Named(Interface);
    }

    /// <summary>The kind whose providers serve <paramref name="face"/>, the registry's key — named by its interface, as
    /// every kind is.</summary>
    internal @this(System.Type face)
    {
        Interface = face;
        Value = Named(face);
    }

    public override string ToString() => Value;

    // ISigning → signing
    private static string Named(System.Type face) => face.Name[1..].ToLowerInvariant();
}
