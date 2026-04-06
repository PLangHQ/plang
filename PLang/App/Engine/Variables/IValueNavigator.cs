namespace App.Engine.Variables;

/// <summary>
/// Navigates into a value by key/index.
/// Each implementation handles one "shape" of data (dict, list, CLR object, JSON string).
/// </summary>
public interface IValueNavigator
{
    bool CanNavigate(object value);
    object? GetProperty(object value, string key);
}
