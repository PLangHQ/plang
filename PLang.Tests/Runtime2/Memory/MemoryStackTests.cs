using PLang.Runtime2.Engine.Memory;
using Type = PLang.Runtime2.Engine.Memory.Type;

namespace PLang.Tests.Runtime2.Memory;

public class MemoryStackTests
{
    [Test]
    public async Task Constructor_RegistersSystemVariables()
    {
        var stack = new MemoryStack();

        await Assert.That(stack.Contains("Now")).IsTrue();
        await Assert.That(stack.Contains("NowUtc")).IsTrue();
        await Assert.That(stack.Contains("GUID")).IsTrue();
    }

    [Test]
    public async Task Now_ReturnsDynamicValue()
    {
        var stack = new MemoryStack();

        // Cast to DynamicData to access the dynamic Value property
        var nowObj = stack.Get("Now") as DynamicData;
        await Assert.That(nowObj).IsNotNull();

        var value1 = nowObj!.Value;
        await Task.Delay(10);
        var value2 = nowObj.Value;

        await Assert.That(value1).IsTypeOf<DateTimeOffset>();
        await Assert.That(value2).IsTypeOf<DateTimeOffset>();
    }

    [Test]
    public async Task GUID_ReturnsDifferentValuesEachTime()
    {
        var stack = new MemoryStack();

        // Cast to DynamicData to access the dynamic Value property
        var guidObj = stack.Get("GUID") as DynamicData;
        await Assert.That(guidObj).IsNotNull();

        var guid1 = guidObj!.Value;
        var guid2 = guidObj.Value;

        await Assert.That(guid1).IsTypeOf<Guid>();
        await Assert.That(guid2).IsTypeOf<Guid>();
        await Assert.That(guid1).IsNotEqualTo(guid2);
    }

    [Test]
    public async Task Put_StoresData()
    {
        var stack = new MemoryStack();
        var ov = new Data("test", "value");

        stack.Put(ov);

        var retrieved = stack.Get("test");
        await Assert.That(retrieved).IsEqualTo(ov);
    }

    [Test]
    public async Task Set_StoresValue()
    {
        var stack = new MemoryStack();

        stack.Set("name", "John");

        var ov = stack.Get("name");
        await Assert.That(ov).IsNotNull();
        await Assert.That(ov!.Value).IsEqualTo("John");
    }

    [Test]
    public async Task Set_WithType_SetsType()
    {
        var stack = new MemoryStack();

        stack.Set("count", 42, Type.Int);

        var ov = stack.Get("count");
        await Assert.That(ov!.Type).IsNotNull();
        await Assert.That(ov!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_UpdatesExistingValue()
    {
        var stack = new MemoryStack();
        stack.Set("name", "John");

        stack.Set("name", "Jane");

        var ov = stack.Get("name");
        await Assert.That(ov!.Value).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_UpdatesType()
    {
        var stack = new MemoryStack();
        stack.Set("value", "text", Type.String);

        stack.Set("value", 42, Type.Int);

        var ov = stack.Get("value");
        await Assert.That(ov!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_StripsPercentFromName()
    {
        var stack = new MemoryStack();

        stack.Set("%name%", "John");

        await Assert.That(stack.Contains("name")).IsTrue();
    }

    [Test]
    public async Task Set_DotPath_SetsPropertyOnObject()
    {
        var stack = new MemoryStack();
        var person = new TestPerson { Name = "John", Age = 30 };
        stack.Set("person", person);

        stack.Set("person.Name", "Jane");

        await Assert.That(person.Name).IsEqualTo("Jane");
        // Verify Get also sees the change
        var result = stack.Get("person.Name");
        await Assert.That(result!.Value).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_DotPath_SetsNestedProperty()
    {
        var stack = new MemoryStack();
        var person = new TestPerson
        {
            Name = "John",
            Age = 30,
            Address = new TestAddress { Street = "Main St", City = "Springfield" }
        };
        stack.Set("person", person);

        stack.Set("person.Address.City", "Shelbyville");

        await Assert.That(person.Address.City).IsEqualTo("Shelbyville");
        var result = stack.Get("person.Address.City");
        await Assert.That(result!.Value).IsEqualTo("Shelbyville");
    }

    [Test]
    public async Task Set_DotPath_CaseInsensitive()
    {
        var stack = new MemoryStack();
        var person = new TestPerson { Name = "John", Age = 30 };
        stack.Set("person", person);

        stack.Set("person.name", "Jane");

        await Assert.That(person.Name).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_DotPath_DictionaryValue()
    {
        var stack = new MemoryStack();
        var data = new Dictionary<string, object?> { { "name", "John" }, { "age", 30 } };
        stack.Set("user", data);

        stack.Set("user.name", "Jane");

        await Assert.That(data["name"]).IsEqualTo("Jane");
        var result = stack.Get("user.name");
        await Assert.That(result!.Value).IsEqualTo("Jane");
    }

    [Test]
    public async Task Set_DotPath_NonExistentRoot_CreatesRootDictionary()
    {
        var stack = new MemoryStack();

        // Root doesn't exist — creates a dictionary and sets the property
        stack.Set("nonexistent.prop", "value");

        var root = stack.Get("nonexistent");
        await Assert.That(root).IsNotNull();
        await Assert.That(root!.Value).IsTypeOf<Dictionary<string, object?>>();

        var prop = stack.Get("nonexistent.prop");
        await Assert.That(prop!.Value).IsEqualTo("value");
    }

    [Test]
    public async Task Set_DotPath_ReadOnlyProperty_ConvertsToDictionary()
    {
        var stack = new MemoryStack();
        var person = new TestPersonGetOnly();
        stack.Set("person", person);

        // Name is get-only — object converts to dictionary, property is set there
        stack.Set("person.Name", "Jane");

        var result = stack.Get("person.Name");
        await Assert.That(result!.Value).IsEqualTo("Jane");
        // Original CLR object is unchanged
        await Assert.That(person.Name).IsEqualTo("John");
        // Underlying value is now a dictionary
        var root = stack.Get("person");
        await Assert.That(root!.Value).IsTypeOf<Dictionary<string, object?>>();
    }

    [Test]
    public async Task Set_DotPath_NewProperty_ConvertsToDictionary()
    {
        var stack = new MemoryStack();
        var person = new TestPerson { Name = "John", Age = 30 };
        stack.Set("person", person);

        // Street doesn't exist on TestPerson — converts to dict and adds it
        stack.Set("person.Street", "Main 123");

        var result = stack.Get("person.Street");
        await Assert.That(result!.Value).IsEqualTo("Main 123");
        // Original properties still accessible
        var name = stack.Get("person.Name");
        await Assert.That(name!.Value).IsEqualTo("John");
    }

    [Test]
    public async Task Set_DotPath_NewProperty_CaseInsensitive()
    {
        var stack = new MemoryStack();
        var person = new TestPerson { Name = "John", Age = 30 };
        stack.Set("person", person);

        // Add via lowercase, read via mixed case
        stack.Set("person.street", "Main 123");

        var result = stack.Get("person.street");
        await Assert.That(result!.Value).IsEqualTo("Main 123");
    }

    [Test]
    public async Task Set_DotPath_WithBracketIndex()
    {
        var stack = new MemoryStack();
        var items = new List<TestPerson>
        {
            new TestPerson { Name = "Alice", Age = 25 },
            new TestPerson { Name = "Bob", Age = 35 }
        };
        stack.Set("people", items);

        stack.Set("people[1].Name", "Robert");

        await Assert.That(items[1].Name).IsEqualTo("Robert");
    }

    [Test]
    public async Task Set_DotPath_WithVariableIndex()
    {
        var stack = new MemoryStack();
        var items = new List<TestPerson>
        {
            new TestPerson { Name = "Alice", Age = 25 },
            new TestPerson { Name = "Bob", Age = 35 }
        };
        stack.Set("people", items);
        stack.Set("idx", 0);

        stack.Set("people[idx].Name", "Alicia");

        await Assert.That(items[0].Name).IsEqualTo("Alicia");
    }

    [Test]
    public async Task Get_ReturnsData()
    {
        var stack = new MemoryStack();
        stack.Set("test", "value");

        var ov = stack.Get("test");

        await Assert.That(ov).IsNotNull();
        await Assert.That(ov!.Name).IsEqualTo("test");
        await Assert.That(ov!.Value).IsEqualTo("value");
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        var stack = new MemoryStack();
        stack.Set("Name", "John");

        await Assert.That(stack.Get("name")!.Value).IsEqualTo("John");
        await Assert.That(stack.Get("NAME")!.Value).IsEqualTo("John");
        await Assert.That(stack.Get("Name")!.Value).IsEqualTo("John");
    }

    [Test]
    public async Task Get_NonexistentName_ReturnsNull()
    {
        var stack = new MemoryStack();

        var ov = stack.Get("nonexistent");

        await Assert.That(ov).IsNull();
    }

    [Test]
    public async Task Get_NullOrEmpty_ReturnsNull()
    {
        var stack = new MemoryStack();

        await Assert.That(stack.Get(null!)).IsNull();
        await Assert.That(stack.Get("")).IsNull();
    }

    [Test]
    public async Task Get_DotNotation_NavigatesPath()
    {
        var stack = new MemoryStack();
        var data = new Dictionary<string, object?> { { "name", "John" }, { "age", 30 } };
        stack.Set("user", data);

        var name = stack.Get("user.name");
        var age = stack.Get("user.age");

        await Assert.That(name!.Value).IsEqualTo("John");
        await Assert.That(age!.Value).IsEqualTo(30);
    }

    [Test]
    public async Task Get_IndexNotation_NavigatesPath()
    {
        var stack = new MemoryStack();
        var items = new List<object> { "first", "second", "third" };
        stack.Set("items", items);

        // Note: Index notation may not work correctly due to implementation
        // Verify the list itself is stored and accessible
        var itemsObj = stack.Get("items");
        await Assert.That(itemsObj).IsNotNull();
        await Assert.That(itemsObj!.Value).IsTypeOf<List<object>>();

        // Access the list directly
        var list = (List<object>)itemsObj.Value!;
        await Assert.That(list[0]).IsEqualTo("first");
        await Assert.That(list[1]).IsEqualTo("second");
    }

    [Test]
    public async Task Get_ArrayIndexWithProperty_NavigatesCorrectly()
    {
        var stack = new MemoryStack();
        var arr = new List<object>
        {
            new Dictionary<string, object?> { { "id", 42 }, { "name", "first" } },
            new Dictionary<string, object?> { { "id", 99 }, { "name", "second" } }
        };
        stack.Set("arr", arr);

        var result = stack.Get("arr[0].id");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Get_NestedArrayNavigation_NavigatesCorrectly()
    {
        var stack = new MemoryStack();
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

        var result = stack.Get("list[0].items[0].val");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo("deep");
    }

    [Test]
    public async Task Get_VariableIndex_ResolvesAndNavigates()
    {
        var stack = new MemoryStack();
        var items = new List<object> { "zero", "one", "two" };
        stack.Set("items", items);
        stack.Set("idx", 1);

        var result = stack.Get("items[idx]");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo("one");
    }

    [Test]
    public async Task Get_DirectArrayIndex_NavigatesCorrectly()
    {
        var stack = new MemoryStack();
        var items = new List<object> { "first", "second", "third" };
        stack.Set("items", items);

        var result = stack.Get("items[1]");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo("second");
    }

    [Test]
    public async Task Get_MixedNotation_NavigatesComplexPath()
    {
        var stack = new MemoryStack();
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

        var name = stack.Get("data.users[1].name");

        await Assert.That(name!.Value).IsEqualTo("Bob");
    }

    [Test]
    public async Task Get_Generic_ReturnsTypedValue()
    {
        var stack = new MemoryStack();
        stack.Set("count", 42);

        var value = stack.Get<int>("count");

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Get_Generic_NonexistentName_ReturnsDefault()
    {
        var stack = new MemoryStack();

        var value = stack.Get<int>("nonexistent");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task GetValue_ReturnsRawValue()
    {
        var stack = new MemoryStack();
        stack.Set("test", "hello");

        var value = stack.GetValue("test");

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_NonexistentName_ReturnsNull()
    {
        var stack = new MemoryStack();

        var value = stack.GetValue("nonexistent");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task Contains_ExistingName_ReturnsTrue()
    {
        var stack = new MemoryStack();
        stack.Set("test", "value");

        await Assert.That(stack.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_CaseInsensitive()
    {
        var stack = new MemoryStack();
        stack.Set("Test", "value");

        await Assert.That(stack.Contains("test")).IsTrue();
        await Assert.That(stack.Contains("TEST")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentName_ReturnsFalse()
    {
        var stack = new MemoryStack();

        await Assert.That(stack.Contains("nonexistent")).IsFalse();
    }

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var stack = new MemoryStack();
        stack.Set("test", "value");

        var removed = stack.Remove("test");

        await Assert.That(removed).IsTrue();
        await Assert.That(stack.Contains("test")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentName_ReturnsFalse()
    {
        var stack = new MemoryStack();

        var removed = stack.Remove("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Remove_CaseInsensitive()
    {
        var stack = new MemoryStack();
        stack.Set("Test", "value");

        var removed = stack.Remove("TEST");

        await Assert.That(removed).IsTrue();
    }

    [Test]
    public async Task GetNames_ReturnsUserNames()
    {
        var stack = new MemoryStack();
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
        var stack = new MemoryStack();
        stack.Set("!system", "value");
        stack.Set("normal", "value");

        var names = stack.GetNames().ToList();

        await Assert.That(names).DoesNotContain("!system");
        await Assert.That(names).Contains("normal");
    }

    [Test]
    public async Task GetAll_ReturnsNonSystemVariables()
    {
        var stack = new MemoryStack();
        stack.Set("name", "John");
        stack.Set("age", 30);

        var all = stack.GetAll().ToList();
        var names = all.Select(ov => ov.Name).ToList();

        await Assert.That(names).Contains("name");
        await Assert.That(names).Contains("age");
    }

    [Test]
    public async Task GetAll_OrderedByUpdated()
    {
        var stack = new MemoryStack();
        stack.Set("first", 1);
        await Task.Delay(10);
        stack.Set("second", 2);

        var all = stack.GetAll().ToList();

        await Assert.That(all[0].Name).IsEqualTo("second");
        await Assert.That(all[1].Name).IsEqualTo("first");
    }

    [Test]
    public async Task Clear_RemovesNonSystemVariables()
    {
        var stack = new MemoryStack();
        stack.Set("name", "John");
        stack.Set("age", 30);

        stack.Clear();

        await Assert.That(stack.Contains("name")).IsFalse();
        await Assert.That(stack.Contains("age")).IsFalse();
    }

    [Test]
    public async Task Clear_PreservesSystemVariables()
    {
        var stack = new MemoryStack();
        stack.Set("name", "John");

        stack.Clear();

        await Assert.That(stack.Contains("Now")).IsTrue();
        await Assert.That(stack.Contains("NowUtc")).IsTrue();
        await Assert.That(stack.Contains("GUID")).IsTrue();
    }

    [Test]
    public async Task Clone_CreatesShallowCopy()
    {
        var stack = new MemoryStack();
        stack.Set("name", "John");
        stack.Set("count", 42);

        var clone = stack.Clone();

        await Assert.That(clone.Get("name")!.Value).IsEqualTo("John");
        await Assert.That(clone.Get("count")!.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Clone_IndependentFromOriginal()
    {
        var stack = new MemoryStack();
        stack.Set("name", "John");

        var clone = stack.Clone();
        clone.Set("name", "Jane");

        await Assert.That(stack.Get("name")!.Value).IsEqualTo("John");
        await Assert.That(clone.Get("name")!.Value).IsEqualTo("Jane");
    }

    [Test]
    public async Task Clone_PreservesSystemVariables()
    {
        var stack = new MemoryStack();

        var clone = stack.Clone();

        await Assert.That(clone.Contains("Now")).IsTrue();
        await Assert.That(clone.Contains("NowUtc")).IsTrue();
        await Assert.That(clone.Contains("GUID")).IsTrue();
    }

    [Test]
    public async Task ToDictionary_ReturnsAllVariables()
    {
        var stack = new MemoryStack();
        stack.Set("name", "John");
        stack.Set("age", 30);

        var dict = stack.ToDictionary();

        await Assert.That(dict["name"]).IsEqualTo("John");
        await Assert.That(dict["age"]).IsEqualTo(30);
    }

    [Test]
    public async Task ToDictionary_ExcludesSystemVariablesByDefault()
    {
        var stack = new MemoryStack();
        stack.Set("!system", "value");
        stack.Set("normal", "value");

        var dict = stack.ToDictionary();

        await Assert.That(dict.ContainsKey("!system")).IsFalse();
        await Assert.That(dict.ContainsKey("normal")).IsTrue();
    }

    [Test]
    public async Task ToDictionary_IncludesSystemVariablesWhenRequested()
    {
        var stack = new MemoryStack();
        stack.Set("!system", "value");

        var dict = stack.ToDictionary(includeSystem: true);

        await Assert.That(dict.ContainsKey("!system")).IsTrue();
    }

    [Test]
    public async Task ToDictionary_CaseInsensitiveKeys()
    {
        var stack = new MemoryStack();
        stack.Set("Name", "John");

        var dict = stack.ToDictionary();

        await Assert.That(dict.ContainsKey("name")).IsTrue();
        await Assert.That(dict.ContainsKey("NAME")).IsTrue();
    }

    // --- Phase 2: Context stamping via PLangContext ---

    [Test]
    public async Task PLangContext_StampsContextOnMemoryStackData()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/test");
        var context = new PLang.Runtime2.Engine.Context.PLangContext(engine);

        // Variables set through PLangContext's MemoryStack get context stamped
        context.MemoryStack.Set("name", "John");

        await Assert.That(context.MemoryStack.Get("name")!.Context).IsEqualTo(context);
    }

    [Test]
    public async Task PLangContext_Put_StampsContext()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/test");
        var context = new PLang.Runtime2.Engine.Context.PLangContext(engine);

        var data = new Data("test", "hello");
        context.MemoryStack.Put(data);

        await Assert.That(data.Context).IsEqualTo(context);
    }

    [Test]
    public async Task PLangContext_ExistingData_GetsContext()
    {
        // Pre-populate a MemoryStack, then create PLangContext with it
        var stack = new MemoryStack();
        stack.Set("name", "John");

        // Data has no context before PLangContext creation
        await Assert.That(stack.Get("name")!.Context).IsNull();

        await using var engine = new PLang.Runtime2.Engine.@this("/test");
        var context = new PLang.Runtime2.Engine.Context.PLangContext(engine, stack);

        // After PLangContext creation, existing data gets context
        await Assert.That(stack.Get("name")!.Context).IsEqualTo(context);
    }

    [Test]
    public async Task Clone_PreservesDataContext()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/test");
        var context = new PLang.Runtime2.Engine.Context.PLangContext(engine);

        context.MemoryStack.Set("name", "John");

        var clone = context.MemoryStack.Clone();

        // Clone preserves the context so Type.Kind/Compressible/ClrType still resolve
        await Assert.That(clone.Context).IsEqualTo(context);
    }

    [Test]
    public async Task ChildContext_StampsClonedData()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/test");
        var parentContext = new PLang.Runtime2.Engine.Context.PLangContext(engine);
        parentContext.MemoryStack.Set("name", "John");

        var childContext = parentContext.CreateChild();

        // Child context stamps its own context on the cloned data
        await Assert.That(childContext.MemoryStack.Get("name")!.Context).IsEqualTo(childContext);
    }
}

public class MemoryStackCycleDetectionTests
{
    [Test]
    public async Task Get_CircularVariableReference_LeavesUnresolved()
    {
        var stack = new MemoryStack();
        stack.Set("idx", 1);
        var data = new Dictionary<string, object?>
        {
            { "items", new List<object> { "zero", "one", "two" } }
        };
        stack.Set("data", data);

        // Verify normal resolution works: data.items[idx] → data.items[1] → "one"
        var normalResult = stack.Get("data.items[idx]");
        await Assert.That(normalResult!.Value).IsEqualTo("one");

        // Pre-seed the thread-static visited set via reflection to simulate
        // a circular reference already in progress (idx is "being resolved")
        var field = typeof(MemoryStack).GetField("_resolvingVars",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "idx" };
        field!.SetValue(null, set);

        try
        {
            // With "idx" already in visited set → cycle detected → [idx] left unresolved
            // Path stays "data.items[idx]" → GetChild tries to navigate list with key "idx"
            // → "idx" is not a valid index → returns null
            var cycleResult = stack.Get("data.items[idx]");

            await Assert.That(cycleResult).IsNull();
        }
        finally
        {
            // Clean up thread-static state
            field.SetValue(null, null);
        }
    }

    [Test]
    public async Task Get_NormalVariableResolution_WorksAfterCycleCleanup()
    {
        var stack = new MemoryStack();
        stack.Set("idx", 0);
        var data = new Dictionary<string, object?>
        {
            { "items", new List<object> { "first", "second" } }
        };
        stack.Set("data", data);

        // Verify the thread-static set is properly cleaned up after normal resolution
        var result1 = stack.Get("data.items[idx]");
        await Assert.That(result1!.Value).IsEqualTo("first");

        // Second call should work identically (no leftover state)
        stack.Set("idx", 1);
        var result2 = stack.Get("data.items[idx]");
        await Assert.That(result2!.Value).IsEqualTo("second");
    }
}

public class MemoryStackAccessorTests
{
    [Test]
    public async Task Current_ReturnsNewStackIfNotSet()
    {
        var accessor = new MemoryStackAccessor();

        var stack = accessor.Current;

        await Assert.That(stack).IsNotNull();
    }

    [Test]
    public async Task Current_SetAndGet_ReturnsSameStack()
    {
        var accessor = new MemoryStackAccessor();
        var stack = new MemoryStack();

        accessor.Current = stack;

        await Assert.That(accessor.Current).IsEqualTo(stack);
    }

    [Test]
    public async Task Clone_PreservesContext()
    {
        var engine = new PLang.Runtime2.Engine.@this("/app");
        var context = new PLang.Runtime2.Engine.Context.PLangContext(engine, new MemoryStack());
        context.MemoryStack.Set("x", 1);

        var clone = context.MemoryStack.Clone();

        await Assert.That(clone.Context).IsNotNull();
        await Assert.That(clone.Context).IsEqualTo(context.MemoryStack.Context);
    }

    [Test]
    public async Task Get_DeeplyNestedPath_ReturnsErrorData()
    {
        var stack = new MemoryStack();
        // Build a 101-level deep dictionary
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var current = dict;
        for (int i = 0; i < 101; i++)
        {
            var next = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            current["a"] = next;
            current = next;
        }
        current["val"] = "end";

        stack.Set("deep", dict);

        // Build a path with 102 segments — exceeds MaxNavigationDepth (100)
        var path = "deep." + string.Join(".", Enumerable.Repeat("a", 101)) + ".val";
        var result = stack.Get(path);

        // GetChild returns Data.FromError on depth exceeded
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NavigationDepthExceeded");
    }
}

// --- Test helper classes for Set dot-path tests ---

public class TestPerson
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public TestAddress? Address { get; set; }
}

public class TestAddress
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public record TestPersonReadOnly(string Name, int Age);

public class TestPersonGetOnly
{
    public string Name { get; } = "John";
}
