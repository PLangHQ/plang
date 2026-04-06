using App.FileSystem;
using App.Variables;
using App.Providers;

namespace App.modules.file.providers;

public interface IFileProvider : IProvider
{
    Data Read(Read action);
    Task<Data> Save(Save action);
    Data Delete(Delete action);
    Data Copy(Copy action);
    Data Move(Move action);
    Data List(List action);
    PLangPath Exists(Exists action);
}
