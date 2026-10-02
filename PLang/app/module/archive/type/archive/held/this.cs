namespace app.module.archive.type.archive.held;

/// <summary>
/// What an archive holds — so unpacking gives it back: <c>{type: data}</c>, a Data whole in plang's own format;
/// <c>{type: file, name: a.txt}</c>, a file's contents and its name; <c>{type: folder, name: photos}</c>, a bundle of a
/// folder. A file or a folder unpacks into a folder, under its name.
/// </summary>
[global::app.Attributes.PlangType("held")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "object";
    public static bool Internal => true;

    /// <summary>What type of thing it holds — <c>data</c>, <c>file</c> or <c>folder</c>; its <c>type</c> on the wire
    /// (an item's own Type is its plang type).</summary>
    [Out, Store, System.Text.Json.Serialization.JsonPropertyName("type")]
    public global::app.type.item.text.@this Of { get; }

    /// <summary>Its name, when it has one — a file's or a folder's.</summary>
    [Out, Store] public global::app.type.item.text.@this? Name { get; }

    public @this(string of, string? name = null)
    {
        Of = of;
        Name = name;
    }

    /// <summary>Whether it is a Data whole — what unpacks back into a value, needing no folder.</summary>
    public bool IsData => string.Equals(Of.ToString(), "data", StringComparison.OrdinalIgnoreCase);

    public override bool IsLeaf => false;

    /// <summary>Made from what an archive wrote of it — <c>{type, name}</c>.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this held) return held;
        if (raw is global::app.type.item.dict.@this dict && data.Context is { } context)
        {
            string? of = null, name = null;
            foreach (var entry in dict.Entries(context))
                switch (entry.Name.ToLowerInvariant())
                {
                    case "type": of = entry.Peek()?.ToString(); break;
                    case "name": name = entry.Peek()?.ToString(); break;
                }
            if (of != null) return new @this(of, name);
        }
        data.Fail(new global::app.error.Error("what an archive holds is {type, name}", "HeldInvalid", 400));
        return null;
    }
}
