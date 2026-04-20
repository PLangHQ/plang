namespace App.Attributes;

/// <summary>
/// Declares capabilities an action handler requires at runtime (e.g. "network", "llm",
/// "disk"). Test discovery reflects on handlers referenced in a test's .pr (and sub-goals
/// reached via static goal.call) and unions the capabilities into the test's auto-tag set.
/// Enables tag-based include/exclude filtering (e.g. --test={"exclude":["network"]}).
/// Class-level only; AllowMultiple is false — use params for multi-capability actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequiresCapabilityAttribute : Attribute
{
    public string[] Capabilities { get; }

    public RequiresCapabilityAttribute(params string[] capabilities)
    {
        Capabilities = capabilities ?? Array.Empty<string>();
    }
}
