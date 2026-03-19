using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Data subclass that lazily resolves the default identity from System DataSource.
/// Lives on Actor as a property. Handlers call Update() after changing the default.
/// Auto-creates a "default" identity if none exist on first access.
/// </summary>
public class IdentityData : Data
{
    private readonly Engine.@this _engine;
    private bool _resolved;

    public IdentityData(Engine.@this engine) : base("Identity", null)
    {
        _engine = engine;
    }

    public override object? Value
    {
        get
        {
            if (!_resolved && base.Value == null)
            {
                _resolved = true;
                base.Value = ResolveDefault();
            }
            return base.Value;
        }
        set
        {
            base.Value = value;
            _resolved = true;
        }
    }

    /// <summary>
    /// Updates the cached identity. Called by handlers after changing the default.
    /// </summary>
    public void Update(IdentityVariable? identity)
    {
        Value = identity;
    }

    /// <remarks>
    /// Sync-over-async is safe here: properties can't be async, and PLang runs sequentially
    /// per context with no SynchronizationContext. SQLite I/O is synchronous under the hood.
    /// </remarks>
    private IdentityVariable? ResolveDefault()
    {
        try
        {
            return IdentityVariable.GetOrCreateDefaultAsync(_engine).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
            // Save failure during auto-create/promotion — return null, IdentityData handles null Value
            return null;
        }
    }
}
