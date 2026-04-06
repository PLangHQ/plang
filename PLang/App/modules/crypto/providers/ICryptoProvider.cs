using App.Variables;
using App.Providers;

namespace App.modules.crypto.providers;

public interface ICryptoProvider : IProvider
{
    Data Hash(Hash action);
    Data Verify(Verify action);
}
