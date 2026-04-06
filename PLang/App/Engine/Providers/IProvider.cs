namespace App.Engine.Providers;

/// <summary>
/// Base interface for all pluggable providers.
/// </summary>
public interface IProvider
{
    string Name { get; }
    bool IsDefault { get; set; }
}
