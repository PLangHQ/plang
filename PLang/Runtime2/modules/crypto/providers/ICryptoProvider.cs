using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.crypto.providers;

/// <summary>
/// Pluggable crypto provider interface. Registered via <c>Engine.Providers</c>.
/// PLang developers can replace the default implementation by loading a DLL that implements this interface.
/// </summary>
public interface ICryptoProvider : IProvider
{
    /// <summary>
    /// Hashes raw bytes with the named algorithm.
    /// Returns <c>Data.Ok(byte[])</c> on success, or <c>Data.FromError</c> for unsupported algorithms.
    /// </summary>
    Data Hash(byte[] data, string algorithm);

    /// <summary>
    /// Verifies that <paramref name="data"/> produces <paramref name="expectedHash"/> under the named algorithm.
    /// Returns <c>Data.Ok(bool)</c>.
    /// </summary>
    Data Verify(byte[] data, byte[] expectedHash, string algorithm);
}
