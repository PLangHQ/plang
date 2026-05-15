using app.FileSystem;
using app.Variables;
using app.Code;

namespace app.modules.file.code;

public interface IFile : ICode
{
    Data.@this Read(Read action);
    Task<Data.@this> Save(Save action);
    Data.@this Delete(Delete action);
    Data.@this Copy(Copy action);
    Data.@this Move(Move action);
    Data.@this List(List action);
    Data.@this Exists(Exists action);
}
