namespace App.modules;

/// <summary>
/// Interface for PLang value types that can be created from a string value.
/// Types implementing this get automatic conversion in TypeMapping.TryConvertTo
/// and their ValidValues appear in the builder summary for the LLM.
/// </summary>
public interface IObject
{
    /// <summary>
    /// The validated string value of this object.
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Valid values that can be used to construct this type.
    /// Shown to the LLM in the builder summary as type(val1|val2|...).
    /// </summary>
    static abstract string[] ValidValues { get; }
}
