using PLang.Utils;

namespace PLang.Tests.Utils;

public class HashHelperTests
{
    [Test]
    public async Task Hash_WithSimpleString_ReturnsSha256Hash()
    {
        // Arrange
        var input = "test";

        // Act
        var hash1 = HashHelper.Hash(input);
        var hash2 = HashHelper.Hash(input);

        // Assert
        await Assert.That(hash1).IsNotNull();
        await Assert.That(hash1).IsEqualTo(hash2); // Same input = same hash
        await Assert.That(hash1.Length).IsEqualTo(64); // SHA256 hex string is 64 chars
    }

    [Test]
    public async Task Hash_WithDifferentStrings_ReturnsDifferentHashes()
    {
        // Act
        var hash1 = HashHelper.Hash("hello");
        var hash2 = HashHelper.Hash("world");

        // Assert
        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [Test]
    public async Task Hash_WithObject_SerializesAndHashes()
    {
        // Arrange
        var obj = new { Name = "Test", Value = 42 };

        // Act
        var hash = HashHelper.Hash(obj);

        // Assert
        await Assert.That(hash).IsNotNull();
        await Assert.That(hash.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Hash_WithSameObjectContent_ReturnsSameHash()
    {
        // Arrange
        var obj1 = new { Name = "Test", Value = 42 };
        var obj2 = new { Name = "Test", Value = 42 };

        // Act
        var hash1 = HashHelper.Hash(obj1);
        var hash2 = HashHelper.Hash(obj2);

        // Assert
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task Hash_WithDifferentObjectContent_ReturnsDifferentHashes()
    {
        // Arrange
        var obj1 = new { Name = "Test", Value = 42 };
        var obj2 = new { Name = "Test", Value = 43 };

        // Act
        var hash1 = HashHelper.Hash(obj1);
        var hash2 = HashHelper.Hash(obj2);

        // Assert
        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [Test]
    public async Task Hash_WithEmptyString_ReturnsValidHash()
    {
        // Act
        var hash = HashHelper.Hash("");

        // Assert
        await Assert.That(hash).IsNotNull();
        await Assert.That(hash.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Hash_WithNull_ReturnsValidHash()
    {
        // Act
        var hash = HashHelper.Hash(null!);

        // Assert
        await Assert.That(hash).IsNotNull();
        await Assert.That(hash.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Hash_WithList_ReturnsValidHash()
    {
        // Arrange
        var list = new List<string> { "a", "b", "c" };

        // Act
        var hash = HashHelper.Hash(list);

        // Assert
        await Assert.That(hash).IsNotNull();
        await Assert.That(hash.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Hash_IsDeterministic()
    {
        // Arrange
        var input = new { Complex = "data", Number = 123, Nested = new { Inner = "value" } };

        // Act - run multiple times
        var hashes = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            hashes.Add(HashHelper.Hash(input));
        }

        // Assert - all should be identical
        await Assert.That(hashes.Distinct().Count()).IsEqualTo(1);
    }
}
