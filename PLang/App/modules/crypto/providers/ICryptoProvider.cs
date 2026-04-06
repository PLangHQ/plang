using App.Variables;
using App.Providers;

namespace App.modules.crypto.providers;

public interface ICryptoProvider : IProvider
{
    Data.@this Hash(Hash action);
    Data.@this Verify(Verify action);
}
