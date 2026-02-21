using PLang.Runtime2.Engine.Settings;

namespace PLang.Tests.Runtime2.Engine.Settings;

public class ScopeTests
{
    [Test]
    public async Task Set_ThenGet_ReturnsValue()
    {
        var scope = new Scope();

        scope.Set("archive.max", 20L * 1024 * 1024);
        var result = scope.Get("archive.max");

        await Assert.That(result).IsEqualTo(20L * 1024 * 1024);
    }

    [Test]
    public async Task Get_WhenNotSet_ReturnsNull()
    {
        var scope = new Scope();

        var result = scope.Get("archive.max");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Contains_WhenSet_ReturnsTrue()
    {
        var scope = new Scope();

        scope.Set("archive.max", 100L);

        await Assert.That(scope.Contains("archive.max")).IsTrue();
    }

    [Test]
    public async Task Contains_WhenNotSet_ReturnsFalse()
    {
        var scope = new Scope();

        await Assert.That(scope.Contains("archive.max")).IsFalse();
    }

    [Test]
    public async Task Keys_AreCaseInsensitive()
    {
        var scope = new Scope();

        scope.Set("Archive.Max", 42L);
        var result = scope.Get("archive.max");

        await Assert.That(result).IsEqualTo(42L);
    }

    [Test]
    public async Task Set_OverwritesExistingValue()
    {
        var scope = new Scope();

        scope.Set("archive.max", 1L);
        scope.Set("archive.max", 2L);
        var result = scope.Get("archive.max");

        await Assert.That(result).IsEqualTo(2L);
    }
}
