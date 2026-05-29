namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// Type's static Build(value) → kind is the build-time sibling of Resolve.
// Discovered by reflection. Distinct from the action handler's IClass.Build().
// number.Build(3.5)→"decimal", image.Build("a.jpg")→"jpg", path.Build("https://…")→"http".

public class TypeBuildHookTests
{
    [Test] public async Task TypeBuild_DiscoveredByReflection_LikeResolve()
        => throw new global::System.NotImplementedException();

    [Test] public async Task TypeBuild_DistinctFrom_ActionIClassBuild()
        => throw new global::System.NotImplementedException();

    [Test] public async Task TypeBuild_ReturnsNullForUnknownValue_DoesNotThrow()
        => throw new global::System.NotImplementedException();

    [Test] public async Task TypeBuild_NotInvoked_ForTypesWithoutKind_DateTimeDuration()
        => throw new global::System.NotImplementedException();
}
