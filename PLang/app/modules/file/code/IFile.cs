using app.FileSystem;
using app.Variables;
using app.Code;

namespace app.modules.file.code;

public interface IFile : ICode
{
    data.@this Read(Read action);
    Task<data.@this> Save(Save action);
    data.@this Delete(Delete action);
    data.@this Copy(Copy action);
    data.@this Move(Move action);
    data.@this List(List action);
    data.@this Exists(Exists action);
}
