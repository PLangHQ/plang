using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Modules;
using TypeInfo = PLang.Runtime2.Memory.TypeInfo;

namespace PLang.Tests.Runtime2.Modules;

public class VariableModuleTests
{
    private VariableModule CreateModule(MemoryStack? memoryStack = null, Step? step = null)
    {
        var module = new VariableModule();
        var appContext = new PLangAppContext("/app");
        var context = new PLangContext(appContext, memoryStack ?? new MemoryStack());
        var moduleContext = new ModuleContext
        {
            Context = context,
            Step = step
        };
        module.Initialize(moduleContext);
        return module;
    }

    [Test]
    public async Task Name_ReturnsVariable()
    {
        var module = new VariableModule();

        await Assert.That(module.Name).IsEqualTo("variable");
    }

    [Test]
    public async Task Aliases_ReturnsVarAndVariables()
    {
        var module = new VariableModule();

        var aliases = module.Aliases.ToList();

        await Assert.That(aliases).Contains("var");
        await Assert.That(aliases).Contains("variables");
    }

    [Test]
    public async Task GetMethods_ReturnsAllMethods()
    {
        var module = new VariableModule();

        var methods = module.GetMethods().ToList();

        await Assert.That(methods).Contains("set");
        await Assert.That(methods).Contains("get");
        await Assert.That(methods).Contains("remove");
        await Assert.That(methods).Contains("exists");
        await Assert.That(methods).Contains("clear");
    }

    [Test]
    public async Task ExecuteAsync_Set_SetsVariable()
    {
        var memoryStack = new MemoryStack();
        var module = CreateModule(memoryStack);
        var parameters = new Dictionary<string, object?> { { "name", "testVar" }, { "value", "testValue" } };

        var result = await module.ExecuteAsync("set", parameters);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memoryStack.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Set_WithType_SetsTypeInfo()
    {
        var memoryStack = new MemoryStack();
        var module = CreateModule(memoryStack);
        var parameters = new Dictionary<string, object?> { { "name", "count" }, { "value", 42 }, { "type", "int" } };

        var result = await module.ExecuteAsync("set", parameters);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memoryStack.Get("count")!.TypeInfo!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task ExecuteAsync_Set_ReturnsValueInResult()
    {
        var memoryStack = new MemoryStack();
        var module = CreateModule(memoryStack);
        var parameters = new Dictionary<string, object?> { { "name", "testVar" }, { "value", "testValue" } };

        var result = await module.ExecuteAsync("set", parameters);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Set_InvalidParameters_ReturnsError()
    {
        var module = CreateModule();

        var result = await module.ExecuteAsync("set", null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidParameters");
    }

    [Test]
    public async Task ExecuteAsync_Set_WithVariableSetRequest_Works()
    {
        var memoryStack = new MemoryStack();
        var module = CreateModule(memoryStack);
        var request = new VariableSetRequest("testVar", "testValue");

        var result = await module.ExecuteAsync("set", request);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memoryStack.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Get_ReturnsVariable()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);
        var parameters = new Dictionary<string, object?> { { "name", "testVar" } };

        var result = await module.ExecuteAsync("get", parameters);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Get_WithStringParameter_Works()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("get", "testVar");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Get_ReturnsValueInResult()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("get", "testVar");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Get_MissingName_ReturnsError()
    {
        var module = CreateModule();

        var result = await module.ExecuteAsync("get", null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingName");
    }

    [Test]
    public async Task ExecuteAsync_Get_WithVariableGetRequest_Works()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);
        var request = new VariableGetRequest("testVar");

        var result = await module.ExecuteAsync("get", request);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Remove_RemovesVariable()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("remove", "testVar");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
        await Assert.That(memoryStack.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_Remove_NonexistentVariable_ReturnsFalse()
    {
        var memoryStack = new MemoryStack();
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("remove", "nonexistent");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task ExecuteAsync_Remove_MissingName_ReturnsError()
    {
        var module = CreateModule();

        var result = await module.ExecuteAsync("remove", null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingName");
    }

    [Test]
    public async Task ExecuteAsync_Exists_ExistingVariable_ReturnsTrue()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("exists", "testVar");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task ExecuteAsync_Exists_NonexistentVariable_ReturnsFalse()
    {
        var memoryStack = new MemoryStack();
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("exists", "nonexistent");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task ExecuteAsync_Exists_ReturnsValueInResult()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("exists", "testVar");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task ExecuteAsync_Exists_MissingName_ReturnsError()
    {
        var module = CreateModule();

        var result = await module.ExecuteAsync("exists", null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingName");
    }

    [Test]
    public async Task ExecuteAsync_Clear_ClearsAllVariables()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("var1", "value1");
        memoryStack.Set("var2", "value2");
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("clear", null);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memoryStack.Contains("var1")).IsFalse();
        await Assert.That(memoryStack.Contains("var2")).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_Clear_PreservesSystemVariables()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("userVar", "value");
        var module = CreateModule(memoryStack);

        await module.ExecuteAsync("clear", null);

        await Assert.That(memoryStack.Contains("Now")).IsTrue();
        await Assert.That(memoryStack.Contains("NowUtc")).IsTrue();
        await Assert.That(memoryStack.Contains("GUID")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_UnknownMethod_ReturnsError()
    {
        var module = CreateModule();

        var result = await module.ExecuteAsync("unknown", null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnknownMethod");
    }

    [Test]
    public async Task ExecuteAsync_CaseInsensitiveMethod()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);

        var result = await module.ExecuteAsync("GET", "testVar");

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_Set_WithJsonElement_Works()
    {
        var memoryStack = new MemoryStack();
        var module = CreateModule(memoryStack);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            "{\"name\": \"testVar\", \"value\": \"testValue\"}");

        var result = await module.ExecuteAsync("set", json);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(memoryStack.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task ExecuteAsync_Get_WithJsonElement_Works()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            "{\"name\": \"testVar\"}");

        var result = await module.ExecuteAsync("get", json);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_Get_WithJsonStringElement_Works()
    {
        var memoryStack = new MemoryStack();
        memoryStack.Set("testVar", "testValue");
        var module = CreateModule(memoryStack);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("\"testVar\"");

        var result = await module.ExecuteAsync("get", json);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("testValue");
    }
}

public class VariableSetRequestTests
{
    [Test]
    public async Task Constructor_SetsProperties()
    {
        var request = new VariableSetRequest("name", "value", "string");

        await Assert.That(request.Name).IsEqualTo("name");
        await Assert.That(request.Value).IsEqualTo("value");
        await Assert.That(request.Type).IsEqualTo("string");
    }

    [Test]
    public async Task Type_DefaultsToNull()
    {
        var request = new VariableSetRequest("name", "value");

        await Assert.That(request.Type).IsNull();
    }
}

public class VariableGetRequestTests
{
    [Test]
    public async Task Constructor_SetsName()
    {
        var request = new VariableGetRequest("testVar");

        await Assert.That(request.Name).IsEqualTo("testVar");
    }
}
