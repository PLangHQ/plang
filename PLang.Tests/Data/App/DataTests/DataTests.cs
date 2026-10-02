using System.Reflection;
using app;
using app.type.item.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.DataTests;

public class DataTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/DataTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Constructor_WithName_SetsName()
    {
        var ov = new Data("testVar");

        await Assert.That(ov.Name).IsEqualTo("testVar");
    }

    [Test]
    public async Task Constructor_WithValue_SetsValue()
    {
        var ov = _app.Data("test", "hello");

        await Assert.That((await ov.Value())?.ToString()).IsEqualTo("hello");
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task Constructor_WithNullValue_IsInitialized()
    {
        // (object?) forces the value ctor — a bare null binds to the item.@this
        // instance ctor by overload resolution.
        var ov = new Data("test", (object?)null);

        // A null value is the plang null citizen (a real item), not C# null.
        await Assert.That((await ov.Value())!.IsNull).IsTrue();
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task Constructor_WithType_SetsType()
    {
        var type = _app.type.list["text"];

        var ov = _app.Data("test", "hello", type);

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    [Test]
    public async Task Constructor_InfersTypeFromValue()
    {
        var ov = _app.Data("test", 42);

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    [Test]
    public async Task Constructor_KeepsTheNameAsGiven()
    {
        var ov = new Data("varName");

        await Assert.That(ov.Name).IsEqualTo("varName");
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
        var parent = _app.Data("parent", new { Name = "test" });
        var child = new Data("Name", "test", parent: parent, context: _app.actor.list.User.Context);

        await Assert.That(child.Path).IsEqualTo("parent.Name");
    }

    [Test]
    public async Task Path_WithNumericName_UsesBracketNotation()
    {
        var parent = _app.Data("items", new List<int> { 1, 2, 3 });
        var child = new Data("0", 1, parent: parent, context: _app.actor.list.User.Context);

        await Assert.That(child.Path).IsEqualTo("items[0]");
    }

    [Test]
    public async Task Value_Setter_UpdatesValue()
    {
        var ov = new Data("test", context: _app.actor.list.User.Context);

        ov.SetValue("new value");

        await Assert.That((await ov.Value())?.ToString()).IsEqualTo("new value");
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task Value_Setter_UpdatesUpdatedTimestamp()
    {
        var ov = new Data("test", context: _app.actor.list.User.Context);
        var initialUpdated = ov.Updated;
        await Task.Delay(1);

        ov.SetValue("new value");

        await Assert.That(ov.Updated).IsGreaterThan(initialUpdated);
    }

    [Test]
    public async Task Value_Setter_InfersTypeIfNull()
    {
        var ov = new Data("test", context: _app.actor.list.User.Context);

        ov.SetValue(42);

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    [Test]
    public async Task GetValue_Generic_ReturnsTypedValue()
    {
        var ov = _app.Data("test", "hello");

        var value = ov.GetValue<string>();

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_Generic_WrongType_ReturnsDefault()
    {
        var ov = _app.Data("test", "hello");

        var value = ov.GetValue<int>();

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task GetValue_Generic_ConvertibleType_Converts()
    {
        var ov = _app.Data("test", 42);

        // The .NET edge: the door opens, the number lowers ITSELF.
        var value = (await ov.Value() as global::app.type.item.number.@this)!.ToDouble();

        await Assert.That(value).IsEqualTo(42.0);
    }

    [Test]
    public async Task GetValue_ByType_ReturnsConvertedValue()
    {
        var ov = _app.Data("test", "hello");

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
        var ov = _app.Data("test", "value");

        var child = await ov.Get("");

        await Assert.That(child).IsEqualTo(ov);
    }

    [Test]
    public async Task GetChild_NullPath_ReturnsSelf()
    {
        var ov = _app.Data("test", "value");

        var child = await ov.Get((string)null!);

        await Assert.That(child).IsEqualTo(ov);
    }

    [Test]
    public async Task GetChild_DotNotation_NavigatesPath()
    {
        var data = new Dictionary<string, object?>
        {
            { "user", new Dictionary<string, object?> { { "name", "John" } } }
        };
        var ov = _app.Data("data", data);

        var child = await ov.Get("user.name");

        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsEqualTo("John");
    }

    [Test]
    public async Task GetChild_IndexNotation_NavigatesArray()
    {
        var data = new List<object> { "first", "second", "third" };
        var ov = _app.Data("items", data);

        var child = await ov.Get("[1]");

        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsEqualTo("second");
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
        var ov = _app.Data("data", data);

        var child = await ov.Get("users[1].name");

        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsEqualTo("Bob");
    }

    [Test]
    public async Task GetChild_NonexistentPath_ReturnsNotInitialized()
    {
        var data = new Dictionary<string, object?> { { "name", "test" } };
        var ov = _app.Data("data", data);

        var child = await ov.Get("nonexistent");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetChild_OutOfBoundsIndex_ReturnsNotInitialized()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = _app.Data("items", data);

        var child = await ov.Get("[10]");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetChild_NegativeIndex_ReturnsNotInitialized()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = _app.Data("items", data);

        var child = await ov.Get("[-1]");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    [Skip("Navigates an anonymous CLR object parked in item.clr — the reflection navigator misses the carrier and falls back to Data.Name. Resolved by clr removal (foreign objects become :item or a hard error). See clr-removal-epic.")]
    public async Task GetChild_PropertyReflection_AccessesObjectProperty()
    {
        var data = new { Name = "Test", Value = 42 };
        var ov = _app.Data("obj", data);

        var nameChild = await ov.Get("Name");
        var valueChild = await ov.Get("Value");

        await Assert.That((await nameChild!.Value())?.ToString()).IsEqualTo("Test");
        await Assert.That((await valueChild!.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    [Skip("Navigates an anonymous CLR object parked in item.clr — the reflection navigator misses the carrier and falls back to Data.Name. Resolved by clr removal (foreign objects become :item or a hard error). See clr-removal-epic.")]
    public async Task GetChild_CaseInsensitiveProperty_Works()
    {
        var data = new { Name = "Test" };
        var ov = _app.Data("obj", data);

        var child = await ov.Get("name");

        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsEqualTo("Test");
    }

    [Test]
    public async Task GetChild_NullValue_ReturnsNotInitialized()
    {
        var ov = new Data("test", context: _app.actor.list.User.Context);

        var child = await ov.Get("anything");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    // Emptiness is truthiness: one door, Data.ToBooleanAsync.

    [Test]
    public async Task Truthy_NullValue_IsFalse()
    {
        var ov = new Data("test");

        await Assert.That(await ov.ToBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task Truthy_EmptyString_IsFalse()
    {
        var ov = _app.Data("test", "");

        await Assert.That(await ov.ToBooleanAsync()).IsFalse();
    }

    // Whitespace is content: "  " is truthy, as in JS and Python; trimming is explicit.
    [Test]
    public async Task Truthy_WhitespaceString_IsTrue()
    {
        var ov = _app.Data("test", "  ");

        await Assert.That(await ov.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Truthy_NonEmptyValue_IsTrue()
    {
        var ov = _app.Data("test", "hello");

        await Assert.That(await ov.ToBooleanAsync()).IsTrue();
    }

    // An absent Data holds the null item, which answers for it — no IsInitialized check in the door.
    [Test]
    public async Task Truthy_NotInitialized_IsFalse()
    {
        var ov = _app.NotFound("test");

        await Assert.That(await ov.ToBooleanAsync()).IsFalse();
        await Assert.That(ov.ToBoolean()).IsFalse();
        await Assert.That(ov.Success).IsTrue();
    }

    // Presence is a different question: "" and 0 and false are given.
    [Test]
    public async Task HasValue_EmptyStringAndFalse_AreGiven()
    {
        await Assert.That(_app.Data("test", "").HasValue).IsTrue();
        await Assert.That(_app.Data("test", false).HasValue).IsTrue();
        await Assert.That(new Data("test").HasValue).IsFalse();
        await Assert.That(_app.NotFound("test").HasValue).IsFalse();
    }

    [Test]
    public async Task Null_CreatesNullData()
    {
        var ov = _app.Null("test");

        await Assert.That(ov.Name).IsEqualTo("test");
        // Born-native: a present null value carries the null.@this singleton
        // (not a C# null _value). IsInitialized stays true — value, not absence.
        await Assert.That(ReferenceEquals((ov.Peek()), app.type.item.@null.@this.Instance)).IsTrue();
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task NotFound_CreatesUninitializedData()
    {
        var ov = _app.NotFound("missing");

        await Assert.That(ov.Name).IsEqualTo("missing");
        await Assert.That((await ov.Value())!.IsTruthy()).IsFalse();
        await Assert.That(ov.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Null_EmptyName_CreatesNullData()
    {
        var ov = _app.Null();

        await Assert.That(ov.Name).IsEqualTo("");
        await Assert.That(ReferenceEquals((ov.Peek()), app.type.item.@null.@this.Instance)).IsTrue();
    }

    [Test]
    public async Task ToString_WithValue_ReturnsValueString()
    {
        var ov = _app.Data("test", 42);

        var str = ov.ToString();

        await Assert.That(str).IsEqualTo("42");
    }

    [Test]
    public async Task ToString_NullValue_ReturnsNullString()
    {
        var ov = new Data("test");

        var str = ov.ToString();

        // A no-value Data renders the plang null citizen — "null", not "(null)".
        await Assert.That(str).IsEqualTo("null");
    }

    [Test]
    public async Task Parent_WhenSet_IsAccessible()
    {
        var parent = _app.Data("parent", "value");
        var child = new Data("child", "value", parent: parent, context: _app.actor.list.User.Context);

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task Parent_WhenNotSet_IsNull()
    {
        var ov = new Data("test");

        await Assert.That(ov.Parent).IsNull();
    }

    // --- Phase 2: Context + Lazy Type derivation ---

    [Test]
    public async Task Context_WhenSet_PropagesToType()
    {
        await using var engine = new global::app.@this("/test").Testing();
        var context = new global::app.actor.context.@this(engine, engine.actor.list.User);

        // Context propagation: setting Data.Context stamps the embedded Type
        // entity so registry-keyed reads (TypeOf, Compressible, ClrType) work.
        // image/jpeg is image's jpg format.
        var ov = new Data("test", new byte[] { 1, 2 }, engine.type.list.Stamp("image/jpeg", context), context: context);

        await Assert.That(ov.Type!.Name).IsEqualTo("image");
        await Assert.That(engine.type.list.Kind(ov.Type!.kind.Name).type(context).Name).IsEqualTo("image");
    }

    [Test]
    public async Task Type_LazyDerivation_WithContext()
    {
        await using var engine = new global::app.@this("/test").Testing();
        var context = new global::app.actor.context.@this(engine, engine.actor.list.User);

        var ov = new Data("test", "hello", context: context);

        // Type lazily derived through context's Engine.Types
        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.Name).IsEqualTo("text");
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    [Test]
    public async Task Type_LazyDerivation_InvalidatedByValueSetter()
    {
        var ov = _app.Data("test", "hello");

        await Assert.That(ov.Type!.Name).IsEqualTo("text");

        ov.SetValue(42);

        await Assert.That(ov.Type!.Name).IsEqualTo("number");
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    [Test]
    public async Task Type_NullValue_ReturnsNullSentinel()
    {
        // Type is non-null end-to-end; the "no value, no explicit type" state
        // is carried as the synthetic Null entity instead of a literal null,
        // so consumers don't need a Type? null guard.  Wire serialization
        // skips the Null sentinel so the on-wire shape is unchanged.
        var ov = new Data("test");

        await Assert.That(ov.Type.IsNull).IsTrue();
    }

    [Test]
    public async Task Type_ExplicitType_NotOverridden()
    {
        await using var engine = new global::app.@this("/test").Testing();
        var context = new global::app.actor.context.@this(engine, engine.actor.list.User);

        // A declared {image, jpg} (bytes off I/O, unread) survives the ctor — the value isn't
        // re-derived to a bare binary that drops the declaration.
        var explicitType = engine.type.list.Stamp("image/jpeg", context);
        var ov = new Data("test", new byte[] { 1, 2, 3 }, explicitType, context: context);

        await Assert.That(ov.Type!.Name).IsEqualTo("image");
        await Assert.That(ov.Type!.kind.Name).IsEqualTo("jpg");
    }

    [Test]
    public async Task Type_Setter_StampsContext()
    {
        await using var engine = new global::app.@this("/test").Testing();
        var context = new global::app.actor.context.@this(engine, engine.actor.list.User);

        var newType = new Type("text", "plain");
        var ov = new Data("test", "hello", newType, context: context);

        // Type gets context from Data — the type resolves through the registry.
        await Assert.That(engine.type.list[newType, context].Name).IsEqualTo("text");
    }

    [Test]
    public async Task Type_Kind_WithContext()
    {
        await using var engine = new global::app.@this("/test").Testing();
        var context = new global::app.actor.context.@this(engine, engine.actor.list.User);

        var data = new Data("img", new byte[] { 1, 2 }, engine.type.list.Stamp("image/jpeg", context), context: context);

        // image/jpeg is image's jpg format — already-compressed content.
        await Assert.That(data.Type!.Name).IsEqualTo("image");
        await Assert.That(data.Type!.kind.Name).IsEqualTo("jpg");
        await Assert.That(engine.type.list[data.Type!, context].kind.Compressible).IsFalse();
    }

    [Test]
    public async Task Type_Kind_WithoutContext_ReturnsNull()
    {
        var imageType = new Type("image/jpeg");

        // Family-Kind accessor is gone — Kind is the subtype (null when unset).
        await Assert.That(imageType.kind.IsEmpty).IsTrue();
        // A type spelled without the registry declares no format — nothing says it compresses.
        await Assert.That(imageType.kind.Compressible).IsFalse();
    }

    [Test]
    public async Task Type_Compressible_TextKind()
    {
        await using var engine = new global::app.@this("/test").Testing();
        var context = new global::app.actor.context.@this(engine, engine.actor.list.User);

        var data = new Data("txt", "hello", engine.type.list.Stamp("text/plain", context), context: context);

        await Assert.That(data.Type!.Name).IsEqualTo("text");
        await Assert.That(engine.type.list[data.Type!, context].kind.Compressible).IsTrue();
    }

    [Test]
    public async Task GetChild_InheritsContext()
    {
        await using var engine = new global::app.@this("/test").Testing();
        var context = new global::app.actor.context.@this(engine, engine.actor.list.User);

        var data = new Dictionary<string, object?> { { "name", "test" } };
        var ov = new Data("data", data, context: context);

        var child = await ov.Get("name");

        await Assert.That(child.IsInitialized).IsTrue();
        await Assert.That(child.Context).IsEqualTo(context);
    }

    // --- Phase 3: Envelope properties + Out view ---




    [Test]
    public async Task Properties_HasOutAttribute()
    {
        // data-normalize Stage 1: [Out] is the wire whitelist. Properties already
        // ships via Wire's custom Write — the tag aligns the attribute
        // with reality so Stage 2's filter sees it correctly.
        var prop = typeof(Data).GetProperty(nameof(Data.Properties));

        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.GetCustomAttribute<OutAttribute>()).IsNotNull();
    }


    [Test]
    public async Task OutView_ExistsInViewEnum()
    {
        var outValue = View.Out;

        await Assert.That(outValue.ToString()).IsEqualTo("Out");
    }

    [Test]
    public async Task Encrypt_ReturnsSelf_NoCryptoYet()
    {
        var data = _app.Data("", "secret", _app.actor.list.User.Context.App.type.list["text"]);

        var result = data.Encrypt();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decrypt_NonEncrypted_ReturnsSelf()
    {
        var data = _app.Data("", "Hello", _app.actor.list.User.Context.App.type.list["text"]);

        var result = data.Decrypt();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decrypt_EncryptedType_ReturnsSelf_NoCryptoYet()
    {
        // A Data declared as "encrypted" — Decrypt is a no-op until a crypto
        // service exists, returning self.
        var encrypted = _app.Data("", new byte[] { 1, 2 }, new global::app.type.@this("encrypted"));

        var result = encrypted.Decrypt();

        // No crypto service — returns self
        await Assert.That(result).IsEqualTo(encrypted);
    }

    // --- v5: Depth limit tests ---

    [Test]
    public async Task UnwrapJsonElement_DeeplyNestedJson_ThrowsAtDepthLimit()
    {
        // Build valid nested JSON: {"a":{"a":{"a":...}}}  200 levels deep
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 200; i++) sb.Append("{\"a\":");
        sb.Append("1");
        for (int i = 0; i < 200; i++) sb.Append('}');
        var json = sb.ToString();

        // JsonDocument.Parse has MaxDepth=64 by default, so use higher limit for parsing
        var options = new System.Text.Json.JsonDocumentOptions { MaxDepth = 300 };
        using var doc = System.Text.Json.JsonDocument.Parse(json, options);

        var ex = Assert.Throws<InvalidOperationException>(() => new global::app.type.item.serializer.json(_app.actor.list.User.Context).Parse(doc.RootElement));
        await Assert.That(ex.Message).Contains("maximum depth");
    }

    [Test]
    public async Task UnwrapJsonElement_FractionalNumber_DefaultsToDouble()
    {
        // A bare decimal-point literal defaults to double (universal language
        // convention); decimal is opt-in via `as number/decimal`.
        var json = "{\"price\":19.99}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = new global::app.type.item.serializer.json(_app.actor.list.User.Context).Parse(doc.RootElement) as app.type.item.dict.@this;

        await Assert.That(result).IsNotNull();
        // Born-native: a JSON number is a number.@this wrapper; its backing
        // (via ToRaw) is double for a bare decimal-point literal.
        var price = await result!.Get("price", _app.actor.list.User.Context)!.Value();
        await Assert.That(price).IsTypeOf<app.type.item.number.@this>();
        await Assert.That(((app.type.item.number.@this)price!).Clr<object>()).IsEqualTo(19.99d);
    }

    [Test]
    public async Task UnwrapJsonElement_IntegerNumber_ReturnsLong()
    {
        var json = "{\"count\":42}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = new global::app.type.item.serializer.json(_app.actor.list.User.Context).Parse(doc.RootElement) as app.type.item.dict.@this;

        await Assert.That(result).IsNotNull();
        // Born-native: a whole JSON number is a number.@this wrapper backed by long.
        var count = await result!.Get("count", _app.actor.list.User.Context)!.Value();
        await Assert.That(count).IsTypeOf<app.type.item.number.@this>();
        await Assert.That(((app.type.item.number.@this)count!).Clr<object>()).IsEqualTo(42L);
    }

    // Data.Merge deleted — it was a list operation living on Data that lowered to
    // CLR List<Data>, with zero production callers. Merge-by-name belongs on the
    // native list type if/when needed (see todos: merge onto list.@this).
}

public class DynamicDataTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/DynamicDataTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Constructor_CreatesWithFactory()
    {
        var counter = 0;
        var dov = new DynamicData("counter", asker => asker.Ok(++counter), _app.actor.list.User.Context);

        await Assert.That(dov.Name).IsEqualTo("counter");
    }

    [Test]
    public async Task Value_CallsFactoryEachTime()
    {
        var counter = 0;
        var dov = new DynamicData("counter", asker => asker.Ok(++counter), _app.actor.list.User.Context);

        var value1 = await dov.Value();
        var value2 = await dov.Value();
        var value3 = await dov.Value();

        await Assert.That(value1?.ToString()).IsEqualTo("1");
        await Assert.That(value2?.ToString()).IsEqualTo("2");
        await Assert.That(value3?.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task Value_WithType_SetsType()
    {
        var dov = new DynamicData("now", asker => asker.Ok(DateTime.Now), _app.actor.list.User.Context, _app.type.list["datetime"]);

        await Assert.That(dov.Type).IsNotNull();
        await Assert.That(dov.Type!.Name).IsEqualTo("datetime");
    }

    [Test]
    public async Task Value_ReturnsCurrentValue()
    {
        var now = DateTime.UtcNow;
        var dov = new DynamicData("now", asker => asker.Ok(now), _app.actor.list.User.Context);

        await Assert.That(Lower<System.DateTimeOffset>(await dov.Value())).IsEqualTo(now);
    }

    // --- IsVariable tests ---

    [Test]
    public async Task IsVariable_StandardVariable_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.item.text.@this("%var%", new global::app.type.item.template.kind.plang.@this()));
        await Assert.That(d.IsVariable).IsTrue();
    }

    [Test]
    public async Task IsVariable_ShortName_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.item.text.@this("%v%", new global::app.type.item.template.kind.plang.@this()));
        await Assert.That(d.IsVariable).IsTrue();
    }

    [Test]
    public async Task IsVariable_EmptyPercents_ReturnsFalse()
    {
        var d = _app.Data("x", "%%");
        await Assert.That(d.IsVariable).IsFalse();
    }

    [Test]
    public async Task IsVariable_EmbeddedVariable_ReturnsFalse()
    {
        var d = _app.Data("x", "hello %var%");
        await Assert.That(d.IsVariable).IsFalse();
    }

    [Test]
    public async Task IsVariable_VariableWithTrailing_ReturnsFalse()
    {
        var d = _app.Data("x", "%var% + 1");
        await Assert.That(d.IsVariable).IsFalse();
    }

    [Test]
    public async Task IsVariable_NonStringValue_ReturnsFalse()
    {
        var d = _app.Data("x", 42);
        await Assert.That(d.IsVariable).IsFalse();
    }

    [Test]
    public async Task IsVariable_NullValue_ReturnsFalse()
    {
        var d = new Data("x");
        await Assert.That(d.IsVariable).IsFalse();
    }

    // --- HasVariable tests ---

    [Test]
    public async Task HasVariable_EmbeddedVariable_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.item.text.@this("hello %name%", new global::app.type.item.template.kind.plang.@this()));
        await Assert.That(d.HasVariable).IsTrue();
    }

    [Test]
    public async Task HasVariable_MultipleVariables_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.item.text.@this("%a% + %b%", new global::app.type.item.template.kind.plang.@this()));
        await Assert.That(d.HasVariable).IsTrue();
    }

    [Test]
    public async Task HasVariable_SingleVariable_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.item.text.@this("%var%", new global::app.type.item.template.kind.plang.@this()));
        await Assert.That(d.HasVariable).IsTrue();
    }

    [Test]
    public async Task HasVariable_NoVariables_ReturnsFalse()
    {
        var d = _app.Data("x", "no vars");
        await Assert.That(d.HasVariable).IsFalse();
    }

    [Test]
    public async Task HasVariable_EmptyPercents_ReturnsFalse()
    {
        var d = _app.Data("x", "%%");
        await Assert.That(d.HasVariable).IsFalse();
    }

    [Test]
    public async Task HasVariable_NonStringValue_ReturnsFalse()
    {
        var d = _app.Data("x", 42);
        await Assert.That(d.HasVariable).IsFalse();
    }

    [Test]
    public async Task HasVariable_NullValue_ReturnsFalse()
    {
        var d = new Data("x");
        await Assert.That(d.HasVariable).IsFalse();
    }
}
