namespace app.type.item.kind.json;

/// <summary>How json is laid out: formatted (indented, a member a line) or compact (one line).</summary>
[global::app.Attributes.PlangType("layout"), global::app.Attributes.Choice]
public enum layout
{
    formatted,
    compact,
}
