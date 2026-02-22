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

    [Test]
    public async Task Set_NullValue_RemovesKey()
    {
        var scope = new Scope();

        scope.Set("archive.max", 100L);
        scope.Set("archive.max", null!);

        await Assert.That(scope.Contains("archive.max")).IsFalse();
        await Assert.That(scope.Get("archive.max")).IsNull();
    }

    [Test]
    public async Task Clone_CreatesIndependentCopy()
    {
        var scope = new Scope();
        scope.Set("archive.max", 100L);

        var clone = scope.Clone();

        // Clone has the same values
        await Assert.That(clone.Get("archive.max")).IsEqualTo(100L);

        // Writes to clone don't affect original
        clone.Set("archive.max", 999L);
        await Assert.That(scope.Get("archive.max")).IsEqualTo(100L);
        await Assert.That(clone.Get("archive.max")).IsEqualTo(999L);

        // Writes to original don't affect clone
        scope.Set("archive.max", 1L);
        await Assert.That(clone.Get("archive.max")).IsEqualTo(999L);
    }

    [Test]
    public async Task Clone_EmptyScope_ReturnsEmptyScope()
    {
        var scope = new Scope();

        var clone = scope.Clone();

        await Assert.That(clone.Contains("archive.max")).IsFalse();
    }
}
