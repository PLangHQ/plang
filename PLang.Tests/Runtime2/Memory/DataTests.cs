using PLang.Runtime2.Engine.Memory;
using Type = PLang.Runtime2.Engine.Memory.Type;

namespace PLang.Tests.Runtime2.Memory;

public class DataTests
{
    [Test]
    public async Task Constructor_WithName_SetsName()
    {
        var ov = new Data("testVar");

        await Assert.That(ov.Name).IsEqualTo("testVar");
    }

    [Test]
    public async Task Constructor_WithValue_SetsValue()
    {
        var ov = new Data("test", "hello");

        await Assert.That(ov.Value).IsEqualTo("hello");
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task Constructor_WithNullValue_NotInitialized()
    {
        var ov = new Data("test", null);

        await Assert.That(ov.Value).IsNull();
        await Assert.That(ov.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Constructor_WithType_SetsType()
    {
        var type = Type.String;

        var ov = new Data("test", "hello", type);

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Constructor_InfersTypeFromValue()
    {
        var ov = new Data("test", 42);

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Constructor_StripsPercentFromName()
    {
        var ov = new Data("%varName%");

        await Assert.That(ov.Name).IsEqualTo("varName");
    }

    [Test]
    public async Task Constructor_TrimsName()
    {
        var ov = new Data("  spacedName  ");

        await Assert.That(ov.Name).IsEqualTo("spacedName");
    }

    [Test]
    public async Task Constructor_SetsCreatedTimestamp()
    {
        var before = DateTime.UtcNow;

        var ov = new Data("test");

        var after = DateTime.UtcNow;
        await Assert.That(ov.Created).IsGreaterThanOrEqualTo(before);
        await Assert.That(ov.Created).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_InitializesProperties()
    {
        var ov = new Data("test");

        await Assert.That(ov.Properties).IsNotNull();
        await Assert.That(ov.Properties.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Path_WithNoParent_EqualsName()
    {
        var ov = new Data("testVar");

        await Assert.That(ov.Path).IsEqualTo("testVar");
    }

    [Test]
    public async Task Path_WithParent_IncludesParentPath()
    {
        var parent = new Data("parent", new { Name = "test" });
        var child = new Data("Name", "test", parent: parent);

        await Assert.That(child.Path).IsEqualTo("parent.Name");
    }

    [Test]
    public async Task Path_WithNumericName_UsesBracketNotation()
    {
        var parent = new Data("items", new List<int> { 1, 2, 3 });
        var child = new Data("0", 1, parent: parent);

        await Assert.That(child.Path).IsEqualTo("items[0]");
    }

    [Test]
    public async Task Value_Setter_UpdatesValue()
    {
        var ov = new Data("test");

        ov.Value = "new value";

        await Assert.That(ov.Value).IsEqualTo("new value");
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task Value_Setter_UpdatesUpdatedTimestamp()
    {
        var ov = new Data("test");
        var initialUpdated = ov.Updated;
        await Task.Delay(1);

        ov.Value = "new value";

        await Assert.That(ov.Updated).IsGreaterThan(initialUpdated);
    }

    [Test]
    public async Task Value_Setter_InfersTypeIfNull()
    {
        var ov = new Data("test");

        ov.Value = 42;

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task GetValue_Generic_ReturnsTypedValue()
    {
        var ov = new Data("test", "hello");

        var value = ov.GetValue<string>();

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_Generic_WrongType_ReturnsDefault()
    {
        var ov = new Data("test", "hello");

        var value = ov.GetValue<int>();

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task GetValue_Generic_ConvertibleType_Converts()
    {
        var ov = new Data("test", 42);

        var value = ov.GetValue<double>();

        await Assert.That(value).IsEqualTo(42.0);
    }

    [Test]
    public async Task GetValue_ByType_ReturnsConvertedValue()
    {
        var ov = new Data("test", "hello");

        var value = ov.GetValue(typeof(string));

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_ByType_Null_ReturnsNull()
    {
        var ov = new Data("test");

        var value = ov.GetValue(typeof(string));

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task GetChild_EmptyPath_ReturnsSelf()
    {
        var ov = new Data("test", "value");

        var child = ov.GetChild("");

        await Assert.That(child).IsEqualTo(ov);
    }

    [Test]
    public async Task GetChild_NullPath_ReturnsSelf()
    {
        var ov = new Data("test", "value");

        var child = ov.GetChild(null!);

        await Assert.That(child).IsEqualTo(ov);
    }

    [Test]
    public async Task GetChild_DotNotation_NavigatesPath()
    {
        var data = new Dictionary<string, object?>
        {
            { "user", new Dictionary<string, object?> { { "name", "John" } } }
        };
        var ov = new Data("data", data);

        var child = ov.GetChild("user.name");

        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value).IsEqualTo("John");
    }

    [Test]
    public async Task GetChild_IndexNotation_NavigatesArray()
    {
        var data = new List<object> { "first", "second", "third" };
        var ov = new Data("items", data);

        var child = ov.GetChild("[1]");

        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value).IsEqualTo("second");
    }

    [Test]
    public async Task GetChild_MixedNotation_NavigatesComplexPath()
    {
        var data = new Dictionary<string, object?>
        {
            { "users", new List<object>
                {
                    new Dictionary<string, object?> { { "name", "Alice" } },
                    new Dictionary<string, object?> { { "name", "Bob" } }
                }
            }
        };
        var ov = new Data("data", data);

        var child = ov.GetChild("users[1].name");

        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value).IsEqualTo("Bob");
    }

    [Test]
    public async Task GetChild_NonexistentPath_ReturnsNull()
    {
        var data = new Dictionary<string, object?> { { "name", "test" } };
        var ov = new Data("data", data);

        var child = ov.GetChild("nonexistent");

        await Assert.That(child).IsNull();
    }

    [Test]
    public async Task GetChild_OutOfBoundsIndex_ReturnsNull()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = new Data("items", data);

        var child = ov.GetChild("[10]");

        await Assert.That(child).IsNull();
    }

    [Test]
    public async Task GetChild_NegativeIndex_ReturnsNull()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = new Data("items", data);

        var child = ov.GetChild("[-1]");

        await Assert.That(child).IsNull();
    }

    [Test]
    public async Task GetChild_PropertyReflection_AccessesObjectProperty()
    {
        var data = new { Name = "Test", Value = 42 };
        var ov = new Data("obj", data);

        var nameChild = ov.GetChild("Name");
        var valueChild = ov.GetChild("Value");

        await Assert.That(nameChild!.Value).IsEqualTo("Test");
        await Assert.That(valueChild!.Value).IsEqualTo(42);
    }

    [Test]
    public async Task GetChild_CaseInsensitiveProperty_Works()
    {
        var data = new { Name = "Test" };
        var ov = new Data("obj", data);

        var child = ov.GetChild("name");

        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value).IsEqualTo("Test");
    }

    [Test]
    public async Task GetChild_NullValue_ReturnsNull()
    {
        var ov = new Data("test");

        var child = ov.GetChild("anything");

        await Assert.That(child).IsNull();
    }

    [Test]
    public async Task IsEmpty_NullValue_ReturnsTrue()
    {
        var ov = new Data("test");

        await Assert.That(ov.IsEmpty).IsTrue();
    }

    [Test]
    public async Task IsEmpty_EmptyString_ReturnsTrue()
    {
        var ov = new Data("test", "");

        await Assert.That(ov.IsEmpty).IsTrue();
    }

    [Test]
    public async Task IsEmpty_NonEmptyValue_ReturnsFalse()
    {
        var ov = new Data("test", "hello");

        await Assert.That(ov.IsEmpty).IsFalse();
    }

    [Test]
    public async Task IsEmpty_NotInitialized_ReturnsTrue()
    {
        var ov = new Data("test");

        await Assert.That(ov.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Null_CreatesNullData()
    {
        var ov = Data.Null("test");

        await Assert.That(ov.Name).IsEqualTo("test");
        await Assert.That(ov.Value).IsNull();
        await Assert.That(ov.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Null_EmptyName_CreatesNullData()
    {
        var ov = Data.Null();

        await Assert.That(ov.Name).IsEqualTo("");
        await Assert.That(ov.Value).IsNull();
    }

    [Test]
    public async Task ToString_WithValue_ReturnsValueString()
    {
        var ov = new Data("test", 42);

        var str = ov.ToString();

        await Assert.That(str).IsEqualTo("42");
    }

    [Test]
    public async Task ToString_NullValue_ReturnsNullString()
    {
        var ov = new Data("test");

        var str = ov.ToString();

        await Assert.That(str).IsEqualTo("(null)");
    }

    [Test]
    public async Task Parent_WhenSet_IsAccessible()
    {
        var parent = new Data("parent", "value");
        var child = new Data("child", "value", parent: parent);

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task Parent_WhenNotSet_IsNull()
    {
        var ov = new Data("test");

        await Assert.That(ov.Parent).IsNull();
    }
}

public class DynamicDataTests
{
    [Test]
    public async Task Constructor_CreatesWithFactory()
    {
        var counter = 0;
        var dov = new DynamicData("counter", () => ++counter);

        await Assert.That(dov.Name).IsEqualTo("counter");
    }

    [Test]
    public async Task Value_CallsFactoryEachTime()
    {
        var counter = 0;
        var dov = new DynamicData("counter", () => ++counter);

        var value1 = dov.Value;
        var value2 = dov.Value;
        var value3 = dov.Value;

        await Assert.That(value1).IsEqualTo(1);
        await Assert.That(value2).IsEqualTo(2);
        await Assert.That(value3).IsEqualTo(3);
    }

    [Test]
    public async Task Value_WithType_SetsType()
    {
        var dov = new DynamicData("now", () => DateTime.Now, Type.DateTime);

        await Assert.That(dov.Type).IsNotNull();
        await Assert.That(dov.Type!.ClrType).IsEqualTo(typeof(DateTime));
    }

    [Test]
    public async Task Value_ReturnsCurrentValue()
    {
        var now = DateTime.UtcNow;
        var dov = new DynamicData("now", () => now);

        await Assert.That(dov.Value).IsEqualTo(now);
    }
}
