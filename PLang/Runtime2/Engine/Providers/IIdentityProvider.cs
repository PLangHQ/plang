using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Provider interface for identity management.
/// Default: DefaultIdentityProvider (DataSource-backed Ed25519 key pairs).
/// PLang developers can swap for cloud KMS, hardware tokens, etc.
/// </summary>
public interface IIdentityProvider : IProvider
{
    Task<Data> GetAsync(Get action);
    Task<Data> CreateAsync(Create action);
    Task<Data> ArchiveAsync(Archive action);
    Task<Data> UnarchiveAsync(Unarchive action);
    Task<Data> SetDefaultAsync(SetDefault action);
    Task<Data> RenameAsync(Rename action);
    Task<Data> GetAllAsync(GetAll action);
    Task<Data> ExportAsync(Export action);
}
