using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// The file channel — a filesystem channel kind. Mime is derived from the
// extension. Bytes come from `path.ReadBytes` (which holds the AuthGate),
// so the channel does no System.IO of its own — PLNG002 stays clean.
public class FileChannelTests
{
    [Test] public async Task FileChannel_ReadsBytesViaPathReadBytes_AuthGateEnforced() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task FileChannel_Mime_DerivedFromExtension() { throw new System.NotImplementedException("not implemented"); }

    // app/module/file/read.cs:27 + app/type/path/file/this.Operations.cs:61
    // — file.read opens the file channel and calls channel.read; the
    // read-time `Context.App.Type.Convert(...)` in FilePath.ReadText goes
    // away.
    [Test] public async Task FileRead_OpensFileChannel_NoReadTimeConvertInFilePathReadText() { throw new System.NotImplementedException("not implemented"); }
}
