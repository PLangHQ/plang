using App.Variables;

namespace App.Data.Navigators;

/// <summary>
/// Navigates a Data object's content by key.
/// Each type can have a registered navigator that knows how to traverse it.
/// </summary>
public interface INavigator
{
    /// <summary>
    /// Navigate into the data's content by key. Returns null if the key doesn't exist.
    /// </summary>
    object? Navigate(@this data, string key);
}
