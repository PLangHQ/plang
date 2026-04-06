using App.Engine.Variables;
using Type = App.Engine.Variables.Type;

namespace PLang.Tests.App.Memory;

public class PropertiesTests
{
    [Test]
    public async Task Constructor_CreatesEmptyCollection()
    {
        var props = new Properties();

        await Assert.That(props.Count).IsEqualTo(0);
        await Assert.That(props.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task Add_AddsData()
    {
        var props = new Properties();
        var ov = new Data("name", "John");

        props.Add(ov);

        await Assert.That(props.Count).IsEqualTo(1);
        await Assert.That(props[0]).IsEqualTo(ov);
    }

    [Test]
    public async Task IndexerByInt_ReturnsItemAtIndex()
    {
        var props = new Properties();
        var ov1 = new Data("first", 1);
        var ov2 = new Data("second", 2);
        props.Add(ov1);
        props.Add(ov2);

        await Assert.That(props[0]).IsEqualTo(ov1);
        await Assert.That(props[1]).IsEqualTo(ov2);
    }

    [Test]
    public async Task IndexerByInt_Set_ReplacesItem()
    {
        var props = new Properties();
        var ov1 = new Data("first", 1);
        var ov2 = new Data("second", 2);
        props.Add(ov1);

        props[0] = ov2;

        await Assert.That(props[0]).IsEqualTo(ov2);
    }

    [Test]
    public async Task IndexerByString_ReturnsItemByName()
    {
        var props = new Properties();
        var ov = new Data("name", "John");
        props.Add(ov);

        var result = props["name"];

        await Assert.That(result).IsEqualTo(ov);
    }

    [Test]
    public async Task IndexerByString_CaseInsensitive()
    {
        var props = new Properties();
        var ov = new Data("Name", "John");
        props.Add(ov);

        await Assert.That(props["name"]).IsEqualTo(ov);
        await Assert.That(props["NAME"]).IsEqualTo(ov);
        await Assert.That(props["Name"]).IsEqualTo(ov);
    }

    [Test]
    public async Task IndexerByString_NonexistentName_ReturnsNull()
    {
        var props = new Properties();

        var result = props["nonexistent"];

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task IndexerByString_Set_AddsNewItem()
    {
        var props = new Properties();
        var ov = new Data("name", "John");

        props["name"] = ov;

        await Assert.That(props.Count).IsEqualTo(1);
        await Assert.That(props["name"]).IsEqualTo(ov);
    }

    [Test]
    public async Task IndexerByString_Set_ReplacesExistingItem()
    {
        var props = new Properties();
        var ov1 = new Data("name", "John");
        var ov2 = new Data("name", "Jane");
        props.Add(ov1);

        props["name"] = ov2;

        await Assert.That(props.Count).IsEqualTo(1);
        await Assert.That(props["name"]).IsEqualTo(ov2);
    }

    [Test]
    public async Task IndexerByString_SetNull_RemovesItem()
    {
        var props = new Properties();
        var ov = new Data("name", "John");
        props.Add(ov);

        props["name"] = null;

        await Assert.That(props.Count).IsEqualTo(0);
        await Assert.That(props["name"]).IsNull();
    }

    [Test]
    public async Task IndexerByString_SetNull_NonexistentItem_DoesNothing()
    {
        var props = new Properties();

        props["nonexistent"] = null;

        await Assert.That(props.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_RemovesAllItems()
    {
        var props = new Properties();
        props.Add(new Data("a", 1));
        props.Add(new Data("b", 2));

        props.Clear();

        await Assert.That(props.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Contains_WithData_ReturnsTrue()
    {
        var props = new Properties();
        var ov = new Data("test", "value");
        props.Add(ov);

        await Assert.That(props.Contains(ov)).IsTrue();
    }

    [Test]
    public async Task Contains_WithData_ReturnsFalse()
    {
        var props = new Properties();
        var ov = new Data("test", "value");

        await Assert.That(props.Contains(ov)).IsFalse();
    }

    [Test]
    public async Task Contains_ByName_ReturnsTrue()
    {
        var props = new Properties();
        props.Add(new Data("test", "value"));

        await Assert.That(props.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_ByName_CaseInsensitive()
    {
        var props = new Properties();
        props.Add(new Data("Test", "value"));

        await Assert.That(props.Contains("test")).IsTrue();
        await Assert.That(props.Contains("TEST")).IsTrue();
    }

    [Test]
    public async Task Contains_ByName_ReturnsFalse()
    {
        var props = new Properties();

        await Assert.That(props.Contains("test")).IsFalse();
    }

    [Test]
    public async Task CopyTo_CopiesItems()
    {
        var props = new Properties();
        var ov1 = new Data("a", 1);
        var ov2 = new Data("b", 2);
        props.Add(ov1);
        props.Add(ov2);
        var array = new Data[3];

        props.CopyTo(array, 1);

        await Assert.That(array[0]).IsNull();
        await Assert.That(array[1]).IsEqualTo(ov1);
        await Assert.That(array[2]).IsEqualTo(ov2);
    }

    [Test]
    public async Task IndexOf_ReturnsCorrectIndex()
    {
        var props = new Properties();
        var ov1 = new Data("a", 1);
        var ov2 = new Data("b", 2);
        props.Add(ov1);
        props.Add(ov2);

        await Assert.That(props.IndexOf(ov1)).IsEqualTo(0);
        await Assert.That(props.IndexOf(ov2)).IsEqualTo(1);
    }

    [Test]
    public async Task IndexOf_NotFound_ReturnsNegative()
    {
        var props = new Properties();
        var ov = new Data("test", "value");

        await Assert.That(props.IndexOf(ov)).IsEqualTo(-1);
    }

    [Test]
    public async Task Insert_InsertsAtPosition()
    {
        var props = new Properties();
        var ov1 = new Data("a", 1);
        var ov2 = new Data("b", 2);
        var ov3 = new Data("c", 3);
        props.Add(ov1);
        props.Add(ov3);

        props.Insert(1, ov2);

        await Assert.That(props[0]).IsEqualTo(ov1);
        await Assert.That(props[1]).IsEqualTo(ov2);
        await Assert.That(props[2]).IsEqualTo(ov3);
    }

    [Test]
    public async Task Remove_RemovesItem()
    {
        var props = new Properties();
        var ov = new Data("test", "value");
        props.Add(ov);

        var removed = props.Remove(ov);

        await Assert.That(removed).IsTrue();
        await Assert.That(props.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_NonexistentItem_ReturnsFalse()
    {
        var props = new Properties();
        var ov = new Data("test", "value");

        var removed = props.Remove(ov);

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task RemoveAt_RemovesAtIndex()
    {
        var props = new Properties();
        props.Add(new Data("a", 1));
        props.Add(new Data("b", 2));
        props.Add(new Data("c", 3));

        props.RemoveAt(1);

        await Assert.That(props.Count).IsEqualTo(2);
        await Assert.That(props[0].Name).IsEqualTo("a");
        await Assert.That(props[1].Name).IsEqualTo("c");
    }

    [Test]
    public async Task Remove_ByName_RemovesItem()
    {
        var props = new Properties();
        props.Add(new Data("test", "value"));

        var removed = props.Remove("test");

        await Assert.That(removed).IsTrue();
        await Assert.That(props.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_ByName_CaseInsensitive()
    {
        var props = new Properties();
        props.Add(new Data("Test", "value"));

        var removed = props.Remove("TEST");

        await Assert.That(removed).IsTrue();
    }

    [Test]
    public async Task Remove_ByName_NonexistentName_ReturnsFalse()
    {
        var props = new Properties();

        var removed = props.Remove("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Get_ReturnsTypedValue()
    {
        var props = new Properties();
        props.Add(new Data("count", 42));

        var value = props.Get<int>("count");

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Get_NonexistentName_ReturnsDefault()
    {
        var props = new Properties();

        var value = props.Get<int>("nonexistent");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Get_WrongType_ReturnsDefault()
    {
        var props = new Properties();
        props.Add(new Data("value", "not a number"));

        var value = props.Get<int>("value");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Set_AddsNewProperty()
    {
        var props = new Properties();

        props.Set("name", "John");

        await Assert.That(props.Count).IsEqualTo(1);
        await Assert.That(props["name"]!.Value).IsEqualTo("John");
    }

    [Test]
    public async Task Set_UpdatesExistingProperty()
    {
        var props = new Properties();
        props.Set("name", "John");

        props.Set("name", "Jane");

        await Assert.That(props.Count).IsEqualTo(1);
        await Assert.That(props["name"]!.Value).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_WithType_SetsType()
    {
        var props = new Properties();

        props.Set("count", 42, Type.Int);

        await Assert.That(props["count"]!.Type).IsNotNull();
        await Assert.That(props["count"]!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_UpdatesExistingType()
    {
        var props = new Properties();
        props.Set("value", "text", Type.String);

        props.Set("value", 42, Type.Int);

        await Assert.That(props["value"]!.Value).IsEqualTo(42);
        await Assert.That(props["value"]!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task ToDictionary_ReturnsAllProperties()
    {
        var props = new Properties();
        props.Add(new Data("name", "John"));
        props.Add(new Data("age", 30));

        var dict = props.ToDictionary();

        await Assert.That(dict.Count).IsEqualTo(2);
        await Assert.That(dict["name"]).IsEqualTo("John");
        await Assert.That(dict["age"]).IsEqualTo(30);
    }

    [Test]
    public async Task ToDictionary_CaseInsensitiveKeys()
    {
        var props = new Properties();
        props.Add(new Data("Name", "John"));

        var dict = props.ToDictionary();

        await Assert.That(dict.ContainsKey("name")).IsTrue();
        await Assert.That(dict.ContainsKey("NAME")).IsTrue();
    }

    [Test]
    public async Task ToDictionary_EmptyProperties_ReturnsEmptyDictionary()
    {
        var props = new Properties();

        var dict = props.ToDictionary();

        await Assert.That(dict.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetEnumerator_EnumeratesAll()
    {
        var props = new Properties();
        props.Add(new Data("a", 1));
        props.Add(new Data("b", 2));

        var count = 0;
        foreach (var item in props)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task GetEnumerator_IEnumerable_EnumeratesAll()
    {
        var props = new Properties();
        props.Add(new Data("a", 1));
        props.Add(new Data("b", 2));

        var count = ((System.Collections.IEnumerable)props).Cast<Data>().Count();

        await Assert.That(count).IsEqualTo(2);
    }
}
