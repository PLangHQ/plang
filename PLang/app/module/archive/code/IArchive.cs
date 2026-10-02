using app.module.code;

namespace app.module.archive.code;

public interface IArchive : ICode
{
    Task<data.@this> Pack(pack action);
    Task<data.@this> Unpack(unpack action);
}
