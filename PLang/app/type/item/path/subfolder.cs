namespace app.type.item.path;

/// <summary>Whether what is under a folder's subfolders is part of a walk of it: included, or skipped.</summary>
[global::app.Attributes.PlangType("subfolder"), global::app.Attributes.Choice]
public enum subfolder
{
    include,
    skip,
}
