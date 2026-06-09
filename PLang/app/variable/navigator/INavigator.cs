namespace app.variable.navigator;

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
    bool CanNavigate(data.@this data);

    /// <summary>
    /// Navigate into the data's content by key. Returns Data.Null(key) if the key doesn't exist.
    /// Async: navigating reads the value through the door (a lazy reference loads here).
    /// </summary>
    System.Threading.Tasks.ValueTask<data.@this> Navigate(data.@this data, string key);
}
