using PLang.Runtime2.Engine.Settings;

namespace PLang.Tests.Runtime2.Engine.Settings;

public class ScopeTests
{
    [Test]
    public void Set_ThenGet_ReturnsValue()
    {
        var scope = new Scope();

        scope.Set("archive.max", 20L * 1024 * 1024);
        var result = scope.Get("archive.max");

        Assert.That(result).IsEqualTo(20L * 1024 * 1024);
    }

    [Test]
    public void Get_WhenNotSet_ReturnsNull()
    {
        var scope = new Scope();

        var result = scope.Get("archive.max");

        Assert.That(result).IsNull();
    }

    [Test]
    public void Contains_WhenSet_ReturnsTrue()
    {
        var scope = new Scope();

        scope.Set("archive.max", 100L);

        Assert.That(scope.Contains("archive.max")).IsTrue();
    }

    [Test]
    public void Contains_WhenNotSet_ReturnsFalse()
    {
        var scope = new Scope();

        Assert.That(scope.Contains("archive.max")).IsFalse();
    }

    [Test]
    public void Keys_AreCaseInsensitive()
    {
        var scope = new Scope();

        scope.Set("Archive.Max", 42L);
        var result = scope.Get("archive.max");

        Assert.That(result).IsEqualTo(42L);
    }
}
