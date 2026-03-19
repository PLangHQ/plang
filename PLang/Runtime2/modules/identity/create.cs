using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Creates a new identity with an Ed25519 key pair.
/// PLang: create identity 'alice', set as default
/// </summary>
[Action("create", Cacheable = false)]
public partial class Create : IContext
{
    [Default("default")]
    public partial string Name { get; init; }

    [Default(false)]
    public partial bool SetAsDefault { get; init; }

    public async Task<Data> Run()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return Data.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        // Check uniqueness across all identities (including archived)
        var all = await IdentityVariable.LoadAllAsync(Context.Engine);
        if (all.Exists(i => string.Equals(i.Name, Name, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{Name}' already exists", "DuplicateName", 409));

        var (publicKey, privateKey) = KeyGenerator.GenerateEd25519();

        // If SetAsDefault, clear existing defaults
        if (SetAsDefault)
        {
            foreach (var existing in all.Where(i => i.IsDefault))
            {
                existing.IsDefault = false;
                var saveResult = await existing.SaveAsync(Context.Engine);
                if (!saveResult.Success) return saveResult;
            }
        }

        var identity = new IdentityVariable
        {
            Name = Name,
            PublicKey = publicKey,
            PrivateKey = privateKey,
            IsDefault = SetAsDefault,
            IsArchived = false,
            Created = DateTime.UtcNow
        };

        var result = await identity.SaveAsync(Context.Engine);
        if (!result.Success) return result;

        if (SetAsDefault)
            Context.Engine.System.Identity.Update(identity);

        return Data.Ok(identity);
    }
}
