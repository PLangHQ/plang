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

    private IdentityVariable? ResolveDefault()
    {
        var all = IdentityVariable.LoadAllAsync(_engine).GetAwaiter().GetResult();
        var def = all.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return def;

        // Auto-create default identity
        var (pub, priv) = KeyGenerator.GenerateEd25519();
        def = new IdentityVariable
        {
            Name = "default",
            PublicKey = pub,
            PrivateKey = priv,
            IsDefault = true,
            IsArchived = false,
            Created = DateTime.UtcNow
        };
        def.SaveAsync(_engine).GetAwaiter().GetResult();
        return def;
    }
}
