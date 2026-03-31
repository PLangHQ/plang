using PLang.Runtime2.Engine.FileSystem;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.file.providers;

public interface IFileProvider : IProvider
{
    Data Read(Read action);
    Task<Data> Save(Save action);
    Data Delete(Delete action);
    Data Copy(Copy action);
    Data Move(Move action);
    Data List(List action);
    PathData Exists(Exists action);
}
