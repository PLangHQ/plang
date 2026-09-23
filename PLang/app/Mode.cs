namespace app;

/// <summary>What the App is doing — building, testing, or running. Derived from what the App holds
/// (<c>Build</c> / <c>Test</c>), never stored beside it; one value, so "building and testing at once"
/// cannot be said.</summary>
[global::app.Attributes.PlangType("mode")]
public enum Mode
{
    Run,
    Build,
    Test,
}
