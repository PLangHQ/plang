namespace app.module.list.type;

/// <summary>What a split does with an empty piece (two separators in a row, one at an end): keeps it, or drops it.</summary>
[global::app.Attributes.PlangType("empty")]
public enum empty
{
    keep,
    drop,
}
