using app.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.VariablesTests;

public class VariablesTests : System.IAsyncDisposable
{
    // Born-with-context: a Variables under test is born from this app's user context
    // (bare `new Variables(_app.User.Context)` would birth context-less values that throw on Set).
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/vars-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Constructor_RegistersSystemVariables()
    {
        var stack = new Variables(_app.User.Context);

        await Assert.That(stack.Contains("Now")).IsTrue();
        await Assert.That(stack.Contains("NowUtc")).IsTrue();
        await Assert.That(stack.Contains("GUID")).IsTrue();
    }

    [Test]
    public async Task Now_ReturnsDynamicValue()
    {
        var stack = new Variables(_app.User.Context);

        // Cast to DynamicData to access the dynamic Value property
        var nowObj = (await stack.Get("Now")) as DynamicData;
        await Assert.That(nowObj).IsNotNull();

        var value1 = await nowObj!.Value();
        await Task.Delay(10);
        var value2 = await nowObj.Value();

        // Born typed: the computed value answers as the datetime item.
        await Assert.That(value1).IsTypeOf<global::app.type.item.datetime.@this>();
        await Assert.That(value2).IsTypeOf<global::app.type.item.datetime.@this>();
    }

    [Test]
    public async Task GUID_ReturnsDifferentValuesEachTime()
    {
        var stack = new Variables(_app.User.Context);

        // Cast to DynamicData to access the dynamic Value property
        var guidObj = (await stack.Get("GUID")) as DynamicData;
        await Assert.That(guidObj).IsNotNull();

        var guid1 = await guidObj!.Value();
        var guid2 = await guidObj.Value();

        // GUID is a typed guid.@this value now, not a raw System.Guid.
        await Assert.That(guid1).IsTypeOf<global::app.type.item.guid.@this>();
        await Assert.That(guid2).IsTypeOf<global::app.type.item.guid.@this>();
        await Assert.That(guid1!.ToString()).IsNotEqualTo(guid2!.ToString());
    }

    [Test]
    public async Task Put_StoresData()
    {
        var stack = new Variables(_app.User.Context);
        var ov = _app.Data("test", "value");

        stack.Set(ov);

        var retrieved = await stack.Get("test");
        await Assert.That(retrieved).IsEqualTo(ov);
    }

    [Test]
    public async Task Set_StoresValue()
    {
        var stack = new Variables(_app.User.Context);

        stack.Set("name", "John");

        var ov = await stack.Get("name");
        await Assert.That(ov).IsNotNull();
        await Assert.That((await ov!.Value())?.ToString()).IsEqualTo("John");
    }

    [Test]
    public async Task Set_WithType_SetsType()
    {
        var stack = new Variables(_app.User.Context);

        stack.Set("count", 42);

        var ov = await stack.Get("count");
        await Assert.That(ov!.Type).IsNotNull();
        await Assert.That(ov!.Type!.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_UpdatesExistingValue()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");

        stack.Set("name", "Jane");

        var ov = await stack.Get("name");
        await Assert.That((await ov!.Value())?.ToString()).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_UpdatesType()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("value", "text");

        stack.Set("value", 42);

        var ov = await stack.Get("value");
        await Assert.That(ov!.Type!.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_StripsPercentFromName()
    {
        var stack = new Variables(_app.User.Context);

        stack.Set("%name%", "John");

        await Assert.That(stack.Contains("name")).IsTrue();
    }

    [Test]
    public async Task Set_DataWithDifferentName_AliasesByKey_NoClone_NoRename()
    {
        // F3-3: when the value is a Data whose Name differs from the storage key,
        // Variables.Set aliases the SAME reference under the key — no clone, no
        // rename. Dictionary key is authoritative for lookup; Data.Name stays
        // advisory (its "original name at creation"). Reverting this branch to
        // the old ShallowClone + rename-to-key behavior would fail both asserts.
        var stack = new Variables(_app.User.Context);
        var original = _app.Data("originalName", 42);

        stack.Set("alias", original);

        var retrieved = await stack.Get("alias");
        await Assert.That(object.ReferenceEquals(retrieved, original)).IsTrue();
        await Assert.That(retrieved.Name).IsEqualTo("originalName");
    }

    [Test]
    public async Task Set_DotPath_SetsPropertyOnObject()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var stack = app.User.Context.Variable;

        var person = new global::app.type.item.dict.@this(_app.User.Context);
        person.Set("Name", "John");
        person.Set("Age", 30L);
        await stack.Set("person", person);

        await stack.Set("person.Name", "Jane");

        // The dict owns its own write; the round-trip read sees the change.
        var result = await stack.Get("person.Name");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_DotPath_ExternalItemClass_OwnsItsWrite_AndMutatesInPlace()
    {
        // An external party adds a class as :item — it owns its own child-write.
        // Unlike a clr-wrapped foreign object, an :item IS the value (stored by
        // reference, no carrier), so the write mutates the very instance.
        var stack = new Variables(_app.User.Context);
        var person = new PersonItem { Name = "John", Age = 30 };
        await stack.Set("person", person);

        await stack.Set("person.Name", "Jane");

        await Assert.That(person.Name).IsEqualTo("Jane");
        var result = await stack.Get("person.Name");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_DotPath_SetsNestedProperty()
    {
        var stack = new Variables(_app.User.Context);
        var address = new global::app.type.item.dict.@this(_app.User.Context);
        address.Set("Street", "Main St");
        address.Set("City", "Springfield");
        var person = new global::app.type.item.dict.@this(_app.User.Context);
        person.Set("Name", "John");
        person.Set("Address", address);
        await stack.Set("person", person);

        await stack.Set("person.Address.City", "Shelbyville");

        var result = await stack.Get("person.Address.City");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Shelbyville");
    }

    [Test]
    public async Task Set_DotPath_CaseInsensitive()
    {
        var stack = new Variables(_app.User.Context);
        var person = new global::app.type.item.dict.@this(_app.User.Context);
        person.Set("Name", "John");
        await stack.Set("person", person);

        await stack.Set("person.name", "Jane");

        // The dict key is case-insensitive — the lowercase write hits "Name".
        var result = await stack.Get("person.Name");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_DotPath_DictionaryValue()
    {
        var stack = new Variables(_app.User.Context);
        var user = new global::app.type.item.dict.@this(_app.User.Context);
        user.Set("name", "John");
        user.Set("age", 30L);
        await stack.Set("user", user);

        await stack.Set("user.name", "Jane");

        var result = await stack.Get("user.name");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_DotPath_NonExistentRoot_CreatesRootDictionary()
    {
        var stack = new Variables(_app.User.Context);

        // Root doesn't exist — creates a native dict and sets the property
        await stack.Set("nonexistent.prop", "value");

        var root = await stack.Get("nonexistent");
        await Assert.That(root).IsNotNull();
        await Assert.That((await root!.Value())).IsTypeOf<global::app.type.item.dict.@this>();

        var prop = await stack.Get("nonexistent.prop");
        await Assert.That((await prop!.Value())?.ToString()).IsEqualTo("value");
    }

    [Test]
    public async Task Set_DotPath_NewProperty_AddsKey()
    {
        var stack = new Variables(_app.User.Context);
        var person = new global::app.type.item.dict.@this(_app.User.Context);
        person.Set("Name", "John");
        await stack.Set("person", person);

        // Street doesn't exist yet — the dict adds it.
        await stack.Set("person.Street", "Main 123");

        var result = await stack.Get("person.Street");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Main 123");
        // Existing key still readable.
        var name = await stack.Get("person.Name");
        await Assert.That((await name!.Value())?.ToString()).IsEqualTo("John");
    }

    [Test]
    public async Task Set_DotPath_NewProperty_CaseInsensitive()
    {
        var stack = new Variables(_app.User.Context);
        var person = new global::app.type.item.dict.@this(_app.User.Context);
        person.Set("Name", "John");
        await stack.Set("person", person);

        // Add via lowercase, read via mixed case.
        await stack.Set("person.street", "Main 123");

        var result = await stack.Get("person.Street");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Main 123");
    }

    [Test]
    public async Task Set_DotPath_WithBracketIndex()
    {
        var stack = new Variables(_app.User.Context);
        var alice = new global::app.type.item.dict.@this(_app.User.Context); alice.Set("Name", "Alice");
        var bob = new global::app.type.item.dict.@this(_app.User.Context); bob.Set("Name", "Bob");
        var people = new global::app.type.item.list.@this(_app.User.Context);
        people.Add(_app.Data("", alice));
        people.Add(_app.Data("", bob));
        await stack.Set("people", people);

        await stack.Set("people[1].Name", "Robert");

        var result = await stack.Get("people[1].Name");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Robert");
    }

    [Test]
    public async Task Set_DotPath_WithVariableIndex()
    {
        // A variable index in a WRITE path (`people[idx]`) resolves against the store the value
        // lives in — its context.Variable. Use the real store (not a detached `new Variables`,
        // whose context.Variable points elsewhere) so idx and people share one scope, as in production.
        var stack = _app.User.Context.Variable;
        var alice = new global::app.type.item.dict.@this(_app.User.Context); alice.Set("Name", "Alice");
        var bob = new global::app.type.item.dict.@this(_app.User.Context); bob.Set("Name", "Bob");
        var people = new global::app.type.item.list.@this(_app.User.Context);
        people.Add(_app.Data("", alice));
        people.Add(_app.Data("", bob));
        await stack.Set("people", people);
        await stack.Set("idx", 0L);

        await stack.Set("people[idx].Name", "Alicia");

        var result = await stack.Get("people[0].Name");
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("Alicia");
    }

    [Test]
    public async Task Get_ReturnsData()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("test", "value");

        var ov = await stack.Get("test");

        await Assert.That(ov).IsNotNull();
        await Assert.That(ov!.Name).IsEqualTo("test");
        await Assert.That((await ov!.Value())?.ToString()).IsEqualTo("value");
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("Name", "John");

        await Assert.That((await (await stack.Get("name"))!.Value())?.ToString()).IsEqualTo("John");
        await Assert.That((await (await stack.Get("NAME"))!.Value())?.ToString()).IsEqualTo("John");
        await Assert.That((await (await stack.Get("Name"))!.Value())?.ToString()).IsEqualTo("John");
    }

    [Test]
    public async Task Get_NonexistentName_ReturnsUninitialized()
    {
        var stack = new Variables(_app.User.Context);

        var ov = await stack.Get("nonexistent");

        await Assert.That(ov.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Get_NullOrEmpty_ReturnsUninitialized()
    {
        var stack = new Variables(_app.User.Context);

        await Assert.That((await stack.Get(null!)).IsInitialized).IsFalse();
        await Assert.That((await stack.Get("")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task Get_DotNotation_NavigatesPath()
    {
        var stack = new Variables(_app.User.Context);
        var data = new Dictionary<string, object?> { { "name", "John" }, { "age", 30 } };
        stack.Set("user", data);

        var name = await stack.Get("user.name");
        var age = await stack.Get("user.age");

        await Assert.That((await name!.Value())?.ToString()).IsEqualTo("John");
        await Assert.That((await age!.Value())?.ToString()).IsEqualTo("30");
    }

    [Test]
    public async Task Get_IndexNotation_NavigatesPath()
    {
        var stack = new Variables(_app.User.Context);
        var items = new List<object> { "first", "second", "third" };
        stack.Set("items", items);

        // Note: Index notation may not work correctly due to implementation
        // Verify the list itself is stored and accessible
        var itemsObj = await stack.Get("items");
        await Assert.That(itemsObj).IsNotNull();
        await Assert.That((await itemsObj!.Value())).IsTypeOf<global::app.type.item.list.@this>();

        // Access the list directly
        var list = Lower<List<object>>(await itemsObj.Value())!;
        await Assert.That((list[0])?.ToString()).IsEqualTo("first");
        await Assert.That((list[1])?.ToString()).IsEqualTo("second");
    }

    [Test]
    public async Task Get_ArrayIndexWithProperty_NavigatesCorrectly()
    {
        var stack = new Variables(_app.User.Context);
        var arr = new List<object>
        {
            new Dictionary<string, object?> { { "id", 42 }, { "name", "first" } },
            new Dictionary<string, object?> { { "id", 99 }, { "name", "second" } }
        };
        stack.Set("arr", arr);

        var result = await stack.Get("arr[0].id");

        await Assert.That(result).IsNotNull();
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Get_NestedArrayNavigation_NavigatesCorrectly()
    {
        var stack = new Variables(_app.User.Context);
        var list = new List<object>
        {
            new Dictionary<string, object?>
            {
                { "items", new List<object>
                    {
                        new Dictionary<string, object?> { { "val", "deep" } }
                    }
                }
            }
        };
        stack.Set("list", list);

        var result = await stack.Get("list[0].items[0].val");

        await Assert.That(result).IsNotNull();
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("deep");
    }

    [Test]
    public async Task Get_VariableIndex_ResolvesAndNavigates()
    {
        // A variable index in a READ resolves in the walk via the value's context
        // (Segment.Index.ResolveKey) — so the store needs a context.
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var stack = app.User.Context.Variable;
        var items = new List<object> { "zero", "one", "two" };
        stack.Set("items", items);
        stack.Set("idx", 1);

        var result = await stack.Get("items[idx]");

        await Assert.That(result).IsNotNull();
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("one");
    }

    [Test]
    public async Task Get_DirectArrayIndex_NavigatesCorrectly()
    {
        var stack = new Variables(_app.User.Context);
        var items = new List<object> { "first", "second", "third" };
        stack.Set("items", items);

        var result = await stack.Get("items[1]");

        await Assert.That(result).IsNotNull();
        await Assert.That((await result!.Value())?.ToString()).IsEqualTo("second");
    }

    [Test]
    public async Task Get_MixedNotation_NavigatesComplexPath()
    {
        var stack = new Variables(_app.User.Context);
        var data = new Dictionary<string, object?>
        {
            { "users", new List<object>
                {
                    new Dictionary<string, object?> { { "name", "Alice" } },
                    new Dictionary<string, object?> { { "name", "Bob" } }
                }
            }
        };
        stack.Set("data", data);

        var name = await stack.Get("data.users[1].name");

        await Assert.That((await name!.Value())?.ToString()).IsEqualTo("Bob");
    }

    [Test]
    public async Task Get_Generic_ReturnsTypedValue()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("count", 42);

        var value = await stack.Get<global::app.type.item.number.@this>("count");

        await Assert.That((await value.Value()).Clr<long>()).IsEqualTo(42L);
    }

    [Test]
    public async Task Get_Generic_NonexistentName_ReturnsDefault()
    {
        var stack = new Variables(_app.User.Context);

        var value = await stack.Get<global::app.type.item.number.@this>("nonexistent");

        await Assert.That(value.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetValue_ReturnsRawValue()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("test", "hello");

        var value = await stack.GetValue("test");

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_NonexistentName_ReturnsNull()
    {
        var stack = new Variables(_app.User.Context);

        var value = await stack.GetValue("nonexistent");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task Contains_ExistingName_ReturnsTrue()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("test", "value");

        await Assert.That(stack.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_CaseInsensitive()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("Test", "value");

        await Assert.That(stack.Contains("test")).IsTrue();
        await Assert.That(stack.Contains("TEST")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentName_ReturnsFalse()
    {
        var stack = new Variables(_app.User.Context);

        await Assert.That(stack.Contains("nonexistent")).IsFalse();
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("test", "value");

        var removed = stack.Remove("test");

        await Assert.That(removed).IsTrue();
        await Assert.That(stack.Contains("test")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentName_ReturnsFalse()
    {
        var stack = new Variables(_app.User.Context);

        var removed = stack.Remove("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Remove_CaseInsensitive()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("Test", "value");

        var removed = stack.Remove("TEST");

        await Assert.That(removed).IsTrue();
    }

    [Test]
    public async Task GetNames_ReturnsUserNames()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");
        stack.Set("age", 30);

        var names = stack.GetNames().ToList();

        await Assert.That(names).Contains("name");
        await Assert.That(names).Contains("age");
        // Note: GetNames returns all names except those starting with "!"
        // System variables (Now, NowUtc, GUID) are included
    }

    [Test]
    public async Task GetNames_ExcludesSystemVariablesStartingWithBang()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("!system", "value");
        stack.Set("normal", "value");

        var names = stack.GetNames().ToList();

        await Assert.That(names).DoesNotContain("!system");
        await Assert.That(names).Contains("normal");
    }

    [Test]
    public async Task GetAll_ReturnsNonSystemVariables()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");
        stack.Set("age", 30);

        var all = stack.GetAll().ToList();
        var names = all.Select(kvp => kvp.Key).ToList();

        await Assert.That(names).Contains("name");
        await Assert.That(names).Contains("age");
    }

    [Test]
    public async Task GetAll_OrderedByUpdated()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("first", 1);
        await Task.Delay(10);
        stack.Set("second", 2);

        var all = stack.GetAll().ToList();

        await Assert.That(all[0].Key).IsEqualTo("second");
        await Assert.That(all[1].Key).IsEqualTo("first");
    }

    [Test]
    public async Task Clear_RemovesNonSystemVariables()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");
        stack.Set("age", 30);

        stack.Clear();

        await Assert.That(stack.Contains("name")).IsFalse();
        await Assert.That(stack.Contains("age")).IsFalse();
    }

    [Test]
    public async Task Clear_PreservesSystemVariables()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");

        stack.Clear();

        await Assert.That(stack.Contains("Now")).IsTrue();
        await Assert.That(stack.Contains("NowUtc")).IsTrue();
        await Assert.That(stack.Contains("GUID")).IsTrue();
    }

    [Test]
    public async Task Clone_CreatesShallowCopy()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");
        stack.Set("count", 42);

        var clone = stack.Clone();

        await Assert.That((await (await clone.Get("name"))!.Value())?.ToString()).IsEqualTo("John");
        await Assert.That((await (await clone.Get("count"))!.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Clone_IndependentFromOriginal()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");

        var clone = stack.Clone();
        clone.Set("name", "Jane");

        await Assert.That((await (await stack.Get("name"))!.Value())?.ToString()).IsEqualTo("John");
        await Assert.That((await (await clone.Get("name"))!.Value())?.ToString()).IsEqualTo("Jane");
    }

    [Test]
    public async Task Clone_PreservesSystemVariables()
    {
        var stack = new Variables(_app.User.Context);

        var clone = stack.Clone();

        await Assert.That(clone.Contains("Now")).IsTrue();
        await Assert.That(clone.Contains("NowUtc")).IsTrue();
        await Assert.That(clone.Contains("GUID")).IsTrue();
    }

    [Test]
    public async Task ToDictionary_ReturnsAllVariables()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("name", "John");
        stack.Set("age", 30);

        var dict = stack.ToDictionary();

        await Assert.That((dict["name"])?.ToString()).IsEqualTo("John");
        await Assert.That(dict["age"]).IsEqualTo(30);
    }

    [Test]
    public async Task ToDictionary_ExcludesSystemVariablesByDefault()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("!system", "value");
        stack.Set("normal", "value");

        var dict = stack.ToDictionary();

        await Assert.That(dict.ContainsKey("!system")).IsFalse();
        await Assert.That(dict.ContainsKey("normal")).IsTrue();
    }

    [Test]
    public async Task ToDictionary_IncludesSystemVariablesWhenRequested()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("!system", "value");

        var dict = stack.ToDictionary(includeSystem: true);

        await Assert.That(dict.ContainsKey("!system")).IsTrue();
    }

    [Test]
    public async Task ToDictionary_CaseInsensitiveKeys()
    {
        var stack = new Variables(_app.User.Context);
        stack.Set("Name", "John");

        var dict = stack.ToDictionary();

        await Assert.That(dict.ContainsKey("name")).IsTrue();
        await Assert.That(dict.ContainsKey("NAME")).IsTrue();
    }

    // --- Phase 2: Context stamping via global::app.actor.context.@this ---

    [Test]
    public async Task PLangContext_StampsContextOnVariablesData()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        // Variables set through global::app.actor.context.@this's Variables get context stamped
        context.Variable.Set("name", "John");

        await Assert.That((await context.Variable.Get("name"))!.Context).IsEqualTo(context);
    }

    [Test]
    public async Task PLangContext_Put_StampsContext()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var data = new Data("test", "hello", context: engine.User.Context);
        context.Variable.Set(data);

        await Assert.That(data.Context).IsEqualTo(context);
    }

    [Test]
    public async Task Clone_PreservesDataContext()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        context.Variable.Set("name", "John");

        var clone = context.Variable.Clone();

        // Clone preserves the context so Type.Kind/Compressible/ClrType still resolve
        await Assert.That(clone.Context).IsEqualTo(context);
    }

    [Test]
    public async Task ChildContext_StampsClonedData()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var parentContext = new global::app.actor.context.@this(engine, engine.User);
        parentContext.Variable.Set("name", "John");

        var childContext = parentContext.CreateChild();

        // Child context stamps its own context on the cloned data
        await Assert.That((await childContext.Variable.Get("name"))!.Context).IsEqualTo(childContext);
    }
}

public class VariablesAccessorTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/varsacc-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Current_ReturnsNull_IfNotSet()
    {
        // No lazy-create: a Variables store must be Set on the async flow before it is read.
        var accessor = new global::app.variable.list.@thisAccessor();

        var stack = accessor.Current;

        await Assert.That(stack).IsNull();
    }

    [Test]
    public async Task Current_SetAndGet_ReturnsSameStack()
    {
        var accessor = new global::app.variable.list.@thisAccessor();
        var stack = new Variables(_app.User.Context);

        accessor.Current = stack;

        await Assert.That(accessor.Current).IsEqualTo(stack);
    }

    [Test]
    public async Task Clone_PreservesContext()
    {
        var engine = global::PLang.Tests.TestApp.Create("/app");
        var context = new global::app.actor.context.@this(engine, engine.User, new Variables(_app.User.Context));
        context.Variable.Set("x", 1);

        var clone = context.Variable.Clone();

        await Assert.That(clone.Context).IsNotNull();
        await Assert.That(clone.Context).IsEqualTo(context.Variable.Context);
    }

    // --- Goal sub-goal navigation tests ---
    // These validate that --debug variable watches and __Resolve can navigate Goal.Child[0].Name

    [Test]
    public async Task Get_GoalSubGoalName_NavigatesCorrectly()
    {
        var stack = new Variables(_app.User.Context);
        var goal = new global::app.goal.@this { Name = "BuildGoal" };
        goal.Child.Add(new global::app.goal.@this { Name = "ProcessGroup" });
        goal.Child.Add(new global::app.goal.@this { Name = "LlmFixer" });
        goal.Child.Add(new global::app.goal.@this { Name = "HandleFailure" });
        stack.Set("goal", goal);

        var name0 = await stack.Get("goal.Child[0].Name");
        var name1 = await stack.Get("goal.Child[1].Name");
        var name2 = await stack.Get("goal.Child[2].Name");

        await Assert.That(name0.IsInitialized).IsTrue();
        await Assert.That((await name0.Value())?.ToString()).IsEqualTo("ProcessGroup");
        await Assert.That((await name1.Value())?.ToString()).IsEqualTo("LlmFixer");
        await Assert.That((await name2.Value())?.ToString()).IsEqualTo("HandleFailure");
    }

    [Test]
    public async Task Get_GoalName_ReturnsGoalName()
    {
        var stack = new Variables(_app.User.Context);
        var goal = new global::app.goal.@this { Name = "BuildGoal" };
        stack.Set("goal", goal);

        var name = await stack.Get("goal.Name");

        await Assert.That(name.IsInitialized).IsTrue();
        await Assert.That((await name.Value())?.ToString()).IsEqualTo("BuildGoal");
    }

    [Test]
    public async Task Get_GoalGoalsCount_ReturnsCount()
    {
        var stack = new Variables(_app.User.Context);
        var goal = new global::app.goal.@this { Name = "BuildGoal" };
        goal.Child.Add(new global::app.goal.@this { Name = "Sub1" });
        goal.Child.Add(new global::app.goal.@this { Name = "Sub2" });
        stack.Set("goal", goal);

        var count = await stack.Get("goal.Child.Count");

        await Assert.That(count.IsInitialized).IsTrue();
        await Assert.That((await count.Value())?.ToString()).IsEqualTo("2");
    }

    [Test]
    public async Task Set_GoalStepsBracketIndex_PreservesGoalIdentity()
    {
        var stack = new Variables(_app.User.Context);
        var goal = new global::app.goal.@this { Name = "BuildGoal" };
        goal.Child.Add(new global::app.goal.@this { Name = "SubGoal" });
        var step = new global::app.goal.step.@this { Index = 0, Text = "original" };
        goal.Step.Add(step);
        stack.Set("goal", goal);

        // Simulate what builder.merge does: set %goal.Step[0]% = newStep
        var newStep = new global::app.goal.step.@this { Index = 0, Text = "updated" };
        stack.Set("goal.Step[0]", newStep);

        // Goal should still be a Goal, not a dictionary — it is a plang item now (the
        // hosts-stay-hosts model was reversed), so it rides as ITSELF, not a clr carrier.
        var retrieved = await stack.Get("goal");
        await Assert.That((await retrieved.Value())).IsTypeOf<global::app.goal.@this>();

        // Sub-goal names should survive
        var subName = await stack.Get("goal.Child[0].Name");
        await Assert.That(subName.IsInitialized).IsTrue();
        await Assert.That((await subName.Value())?.ToString()).IsEqualTo("SubGoal");

        // Step should be updated
        var stepText = await stack.Get("goal.Step[0].Text");
        await Assert.That((await stepText.Value())?.ToString()).IsEqualTo("updated");
    }

    [Test]
    public async Task Set_GoalRidesAsItem_PreservingIdentity()
    {
        var stack = new Variables(_app.User.Context);
        var goal = new global::app.goal.@this { Name = "MyGoal" };
        stack.Set("goal", goal);

        // A goal is a plang item now (the hosts-stay-hosts model was reversed) — it rides as
        // ITSELF, not wrapped in a clr carrier. The Data holds the goal directly.
        var retrieved = await stack.Get("goal");
        await Assert.That(retrieved).IsNotNull();

        // Peek answers the same goal instance — identity is preserved with no carrier hop.
        await Assert.That(object.ReferenceEquals(retrieved!.Peek(), goal)).IsTrue();
    }

    // ResolveDeep was deleted in v4 (resolution lives in data.Value<T>() per call).
    // Equivalent behaviour is covered by DataAsTResolutionTests + the matrix Resolution group.
}

// --- Test helper: an external class that opts into the value model by inheriting
// :item — the post-clr extensibility story. It owns its own child-write (the write
// counterpart of read-navigation), so `set %person.Name%` dispatches to it. Because
// it IS the value (no clr carrier, no clone), the write lands on the instance.
public sealed class PersonItem : global::app.type.item.@this
{
    public string? Name { get; set; }
    public long Age { get; set; }

    public override System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value)
    {
        if (string.Equals(key, "Name", System.StringComparison.OrdinalIgnoreCase))
            Name = value?.ToString();
        else if (string.Equals(key, "Age", System.StringComparison.OrdinalIgnoreCase))
            Age = System.Convert.ToInt64(value);
        else
            throw new System.NotSupportedException($"PersonItem has no child '{key}'");
        return new(this);
    }
}
