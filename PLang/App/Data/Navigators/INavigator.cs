namespace App.Data.Navigators;

/// <summary>
/// Navigates a Data object's content by key.
/// All navigators receive and return Data — never raw objects.
/// Returns Data.Null(key) when the key doesn't exist.
/// </summary>
public interface INavigator
{
    /// <summary>
    /// Whether this navigator can handle the given Data (based on its Value type).
    /// </summary>
    bool CanNavigate(Data.@this data);

    /// <summary>
    /// Navigate into the data's content by key. Returns Data.Null(key) if the key doesn't exist.
    /// </summary>
    Data.@this Navigate(Data.@this data, string key);
}
