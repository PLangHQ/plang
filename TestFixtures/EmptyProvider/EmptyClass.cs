namespace EmptyProvider;

/// <summary>
/// A plain class — not an ICode. Used to test the "no providers found" error path.
/// </summary>
public class NotAProvider
{
    public string Name => "not-a-provider";
}
