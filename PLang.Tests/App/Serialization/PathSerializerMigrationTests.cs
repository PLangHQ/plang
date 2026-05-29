namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2 (path as first mover)
// app/types/path/serializer/Default.cs replaces app/types/path/this.JsonConverter.cs.
// A path value renders identically to the wire before and after the migration.
// path.Build("https://…") → "http" (scheme is path's kind). this.JsonConverter.cs is deleted.

public class PathSerializerMigrationTests
{
    [Test] public async Task PathFile_Wire_RendersAsRelativeString_ViaDefaultSerializer()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PathHttp_Wire_RendersAsAbsoluteString_ViaDefaultSerializer()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PathBuild_HttpsScheme_ReturnsHttpKind()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PathBuild_FileScheme_ReturnsFileKind()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PathBuild_Unknown_ReturnsNull_NoThrow()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LegacyJsonConverter_FileDoesNotExist_AfterMigration()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Path_Wire_ByteForByteParity_BeforeAndAfter_Migration()
        => throw new global::System.NotImplementedException();
}
