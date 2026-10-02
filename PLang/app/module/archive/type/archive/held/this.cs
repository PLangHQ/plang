namespace app.module.archive.type.archive.held;

/// <summary>
/// What an archive holds — so unpacking gives it back: <c>{kind: data}</c>, a Data whole in plang's own format;
/// <c>{kind: file, name: a.txt}</c>, a file's contents and its name; <c>{kind: folder, name: photos}</c>, a bundle of a
/// folder. A file or a folder unpacks into a folder, under its name.
/// </summary>
[global::app.Attributes.PlangType("held")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Description => "What an archive holds: a Data, a file with its name, or a folder.";
    public static string Shape => "object";
    public static bool Internal => true;

    /// <summary>What kind of thing it holds — <c>data</c>, <c>file</c> or <c>folder</c>.</summary>
    [Out, Store] public global::app.type.item.text.@this Kind { get; }

    /// <summary>Its name, when it has one — a file's or a folder's.</summary>
    [Out, Store] public global::app.type.item.text.@this? Name { get; }

    public @this(string kind, string? name = null)
    {
        Kind = kind;
        Name = name;
    }

    /// <summary>Whether it is a Data whole — what unpacks back into a value, needing no folder.</summary>
    public bool IsData => string.Equals(Kind.ToString(), "data", StringComparison.OrdinalIgnoreCase);

    public override bool IsLeaf => false;

    /// <summary>Made from what an archive wrote of it — <c>{type, name}</c>.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this held) return held;
        if (raw is global::app.type.item.dict.@this dict && data.Context is { } context)
        {
            string? kind = null, name = null;
            foreach (var entry in dict.Entries(context))
                switch (entry.Name.ToLowerInvariant())
                {
                    case "kind": kind = entry.Peek()?.ToString(); break;
                    case "name": name = entry.Peek()?.ToString(); break;
                }
            if (kind != null) return new @this(kind, name);
        }
        data.Fail(new global::app.error.Error("what an archive holds is {kind, name}", "HeldInvalid", 400));
        return null;
    }
}
