using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.actions.convert;

namespace PLang.Tests.Runtime2.actions.convert;

public class ConvertTests
{
    private (PLangContext context, MemoryStack memory) CreateContext()
    {
        var engine = new Engine("/app");
        var memory = new MemoryStack();
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    // --- ToInt ---

    [Test]
    public async Task ToInt_FromString()
    {
        var (context, _) = CreateContext();

        var action = new ToInt { Context = context, Value = "42" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
        await Assert.That(result.Value is int).IsTrue();
    }

    [Test]
    public async Task ToInt_FromDouble()
    {
        var (context, _) = CreateContext();

        var action = new ToInt { Context = context, Value = 3.7 };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(4);
    }

    [Test]
    public async Task ToInt_InvalidString_Fails()
    {
        var (context, _) = CreateContext();

        var action = new ToInt { Context = context, Value = "not a number" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- ToLong ---

    [Test]
    public async Task ToLong_FromInt()
    {
        var (context, _) = CreateContext();

        var action = new ToLong { Context = context, Value = 42 };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(42L);
    }

    // --- ToDouble ---

    [Test]
    public async Task ToDouble_FromString()
    {
        var (context, _) = CreateContext();

        var action = new ToDouble { Context = context, Value = "3.14" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Convert.ToDouble(result.Value)).IsEqualTo(3.14);
    }

    // --- ToString ---

    [Test]
    public async Task ToString_FromInt()
    {
        var (context, _) = CreateContext();

        var action = new PLang.Runtime2.actions.convert.ToString
            { Context = context, Value = 42 };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("42");
    }

    [Test]
    public async Task ToString_WithFormat()
    {
        var (context, _) = CreateContext();

        var action = new PLang.Runtime2.actions.convert.ToString
            { Context = context, Value = 3.14159, Format = "F2" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("3.14");
    }

    // --- ToBool ---

    [Test]
    public async Task ToBool_FromTrueString()
    {
        var (context, _) = CreateContext();

        var action = new ToBool { Context = context, Value = "true" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task ToBool_FromFalseString()
    {
        var (context, _) = CreateContext();

        var action = new ToBool { Context = context, Value = "false" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task ToBool_From1()
    {
        var (context, _) = CreateContext();

        var action = new ToBool { Context = context, Value = "1" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(true);
    }

    // --- ToDateTime ---

    [Test]
    public async Task ToDateTime_FromString()
    {
        var (context, _) = CreateContext();

        var action = new ToDateTime { Context = context, Value = "2024-01-15" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is DateTime).IsTrue();
    }

    [Test]
    public async Task ToDateTime_WithFormat()
    {
        var (context, _) = CreateContext();

        var action = new ToDateTime { Context = context, Value = "15/01/2024", Format = "dd/MM/yyyy" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var dt = (DateTime)result.Value!;
        await Assert.That(dt.Day).IsEqualTo(15);
        await Assert.That(dt.Month).IsEqualTo(1);
    }

    // --- ToJson ---

    [Test]
    public async Task ToJson_SerializesObject()
    {
        var (context, _) = CreateContext();

        var obj = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };
        var action = new ToJson { Context = context, Value = obj };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var json = result.Value as string;
        await Assert.That(json).IsNotNull();
        await Assert.That(json!.Contains("Alice")).IsTrue();
    }

    // --- FromJson ---

    [Test]
    public async Task FromJson_ParsesObject()
    {
        var (context, _) = CreateContext();

        var action = new FromJson { Context = context, Value = "{\"name\":\"Alice\",\"age\":30}" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var dict = result.Value as Dictionary<string, object?>;
        await Assert.That(dict).IsNotNull();
        await Assert.That(dict!["name"]).IsEqualTo("Alice");
    }

    [Test]
    public async Task FromJson_InvalidJson_Fails()
    {
        var (context, _) = CreateContext();

        var action = new FromJson { Context = context, Value = "not json" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- ToBase64 / FromBase64 ---

    [Test]
    public async Task ToBase64_EncodesString()
    {
        var (context, _) = CreateContext();

        var action = new ToBase64 { Context = context, Value = "Hello World" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("SGVsbG8gV29ybGQ=");
    }

    [Test]
    public async Task FromBase64_DecodesString()
    {
        var (context, _) = CreateContext();

        var action = new FromBase64 { Context = context, Value = "SGVsbG8gV29ybGQ=" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("Hello World");
    }

    [Test]
    public async Task FromBase64_InvalidBase64_Fails()
    {
        var (context, _) = CreateContext();

        var action = new FromBase64 { Context = context, Value = "not-valid-base64!!!" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }
}
