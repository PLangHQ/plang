namespace app.type.guid;

/// <summary>
/// String → guid. Accepts the canonical 36-char form and the other forms
/// <see cref="System.Guid.TryParse(string, out System.Guid)"/> accepts
/// (braced, hyphenless). Empty/garbage returns null (the caller errors).
/// </summary>
public sealed partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return System.Guid.TryParse(raw.Trim(), out var v) ? new @this(v) : null;
    }
}
