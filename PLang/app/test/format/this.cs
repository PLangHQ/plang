namespace app.test.format;

/// <summary>
/// A test report's format — a format kind that writes a run's report (<c>json</c>, <c>junit</c>), by the kind's own
/// answer (<see cref="global::app.type.kind.@this.Report"/>), never a list of names here. An unknown name is the
/// choice's own error, and the builder sees the options.
/// </summary>
[global::app.Attributes.PlangType("format")]
public sealed class @this
{
    // the format kinds plang declares, the ones that write a report
    private static readonly global::app.type.kind.@this[] Reporting = typeof(@this).Assembly.GetTypes()
        .Where(t => typeof(global::app.type.kind.@this).IsAssignableFrom(t) && t is { IsAbstract: false }
                    && t.GetConstructor(System.Type.EmptyTypes) != null)
        .Select(t => (global::app.type.kind.@this)System.Activator.CreateInstance(t)!)
        .Where(k => k.Report != null)
        .ToArray();

    [global::app.Attributes.Choices]
    public static string[] Choices(actor.context.@this? context) => [.. Reporting.Select(k => k.Name).Order(System.StringComparer.Ordinal)];

    /// <summary>The kind that writes the report.</summary>
    public global::app.type.kind.@this Kind { get; }

    [Out] public string Value { get; }

    public @this(string value)
    {
        Kind = Reporting.FirstOrDefault(k => k.Names(value))
            ?? throw new System.ArgumentException(
                $"Unknown test report format '{value}'. Valid: {string.Join(", ", Choices(null))}");
        Value = Kind.Name;
    }

    public override string ToString() => Value;
}
