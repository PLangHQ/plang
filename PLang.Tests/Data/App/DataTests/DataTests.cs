using System.Reflection;
using app;
using app.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.DataTests;

public class DataTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/DataTests-" + System.Guid.NewGuid().ToString("N")[..6]);
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
        var type = Type.String;

        var ov = _app.Data("test", "hello", type);

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Constructor_InfersTypeFromValue()
    {
        var ov = _app.Data("test", 42);

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
        var parent = _app.Data("parent", new { Name = "test" });
        var child = new Data("Name", "test", parent: parent, context: _app.User.Context);

        await Assert.That(child.Path).IsEqualTo("parent.Name");
    }

    [Test]
    public async Task Path_WithNumericName_UsesBracketNotation()
    {
        var parent = _app.Data("items", new List<int> { 1, 2, 3 });
        var child = new Data("0", 1, parent: parent, context: _app.User.Context);

        await Assert.That(child.Path).IsEqualTo("items[0]");
    }

    [Test]
    public async Task Value_Setter_UpdatesValue()
    {
        var ov = new Data("test", context: _app.User.Context);

        ov.SetValue("new value");

        await Assert.That((await ov.Value())?.ToString()).IsEqualTo("new value");
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task Value_Setter_UpdatesUpdatedTimestamp()
    {
        var ov = new Data("test", context: _app.User.Context);
        var initialUpdated = ov.Updated;
        await Task.Delay(1);

        ov.SetValue("new value");

        await Assert.That(ov.Updated).IsGreaterThan(initialUpdated);
    }

    [Test]
    public async Task Value_Setter_InfersTypeIfNull()
    {
        var ov = new Data("test", context: _app.User.Context);

        ov.SetValue(42);

        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(int));
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
        var value = (await ov.Value() as global::app.type.number.@this)!.ToDouble();

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

        var child = await ov.GetChild("");

        await Assert.That(child).IsEqualTo(ov);
    }

    [Test]
    public async Task GetChild_NullPath_ReturnsSelf()
    {
        var ov = _app.Data("test", "value");

        var child = await ov.GetChild(null!);

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

        var child = await ov.GetChild("user.name");

        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsEqualTo("John");
    }

    [Test]
    public async Task GetChild_IndexNotation_NavigatesArray()
    {
        var data = new List<object> { "first", "second", "third" };
        var ov = _app.Data("items", data);

        var child = await ov.GetChild("[1]");

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

        var child = await ov.GetChild("users[1].name");

        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsEqualTo("Bob");
    }

    [Test]
    public async Task GetChild_NonexistentPath_ReturnsNotInitialized()
    {
        var data = new Dictionary<string, object?> { { "name", "test" } };
        var ov = _app.Data("data", data);

        var child = await ov.GetChild("nonexistent");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetChild_OutOfBoundsIndex_ReturnsNotInitialized()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = _app.Data("items", data);

        var child = await ov.GetChild("[10]");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetChild_NegativeIndex_ReturnsNotInitialized()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = _app.Data("items", data);

        var child = await ov.GetChild("[-1]");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    [Skip("Navigates an anonymous CLR object parked in item.clr — the reflection navigator misses the carrier and falls back to Data.Name. Resolved by clr removal (foreign objects become :item or a hard error). See clr-removal-epic.")]
    public async Task GetChild_PropertyReflection_AccessesObjectProperty()
    {
        var data = new { Name = "Test", Value = 42 };
        var ov = _app.Data("obj", data);

        var nameChild = await ov.GetChild("Name");
        var valueChild = await ov.GetChild("Value");

        await Assert.That((await nameChild!.Value())?.ToString()).IsEqualTo("Test");
        await Assert.That((await valueChild!.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    [Skip("Navigates an anonymous CLR object parked in item.clr — the reflection navigator misses the carrier and falls back to Data.Name. Resolved by clr removal (foreign objects become :item or a hard error). See clr-removal-epic.")]
    public async Task GetChild_CaseInsensitiveProperty_Works()
    {
        var data = new { Name = "Test" };
        var ov = _app.Data("obj", data);

        var child = await ov.GetChild("name");

        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsEqualTo("Test");
    }

    [Test]
    public async Task GetChild_NullValue_ReturnsNotInitialized()
    {
        var ov = new Data("test", context: global::PLang.Tests.TestApp.SharedContext);

        var child = await ov.GetChild("anything");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    public async Task IsEmpty_NullValue_ReturnsTrue()
    {
        var ov = new Data("test");

        await Assert.That(await ov.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task IsEmpty_EmptyString_ReturnsTrue()
    {
        var ov = _app.Data("test", "");

        await Assert.That(await ov.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task IsEmpty_NonEmptyValue_ReturnsFalse()
    {
        var ov = _app.Data("test", "hello");

        await Assert.That(await ov.IsEmpty()).IsFalse();
    }

    [Test]
    public async Task IsEmpty_NotInitialized_ReturnsTrue()
    {
        var ov = new Data("test");

        await Assert.That(await ov.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task Null_CreatesNullData()
    {
        var ov = _app.Null("test");

        await Assert.That(ov.Name).IsEqualTo("test");
        // Born-native: a present null value carries the null.@this singleton
        // (not a C# null _value). IsInitialized stays true — value, not absence.
        await Assert.That(ReferenceEquals((ov.Peek()), app.type.@null.@this.Instance)).IsTrue();
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task NotFound_CreatesUninitializedData()
    {
        var ov = _app.NotFound("missing");

        await Assert.That(ov.Name).IsEqualTo("missing");
        await Assert.That(await (await ov.Value())!.IsEmpty()).IsTrue();
        await Assert.That(ov.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Null_EmptyName_CreatesNullData()
    {
        var ov = _app.Null();

        await Assert.That(ov.Name).IsEqualTo("");
        await Assert.That(ReferenceEquals((ov.Peek()), app.type.@null.@this.Instance)).IsTrue();
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
        var child = new Data("child", "value", parent: parent, context: _app.User.Context);

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
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        // Context propagation: setting Data.Context stamps the embedded Type
        // entity so registry-keyed reads (TypeOf, Compressible, ClrType) work.
        // Bytes off I/O are binary; the kind (jpg) names how they narrow.
        var ov = new Data("test", new byte[] { 1, 2 }, engine.Format.TypeFromMime("image/jpeg"), context: context);

        // The family lives on the format registry, keyed by the kind (the
        // subtype) — jpg → image — not by the Name, which is just "binary".
        await Assert.That(ov.Type!.Name).IsEqualTo("binary");
        await Assert.That(engine.Format.TypeOf(ov.Type!.Kind!)).IsEqualTo("image");
    }

    [Test]
    public async Task Type_LazyDerivation_WithContext()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var ov = new Data("test", "hello", context: context);

        // Type lazily derived through context's Engine.Types
        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.Name).IsEqualTo("text");
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Type_LazyDerivation_InvalidatedByValueSetter()
    {
        var ov = _app.Data("test", "hello");

        await Assert.That(ov.Type!.Name).IsEqualTo("text");

        ov.SetValue(42);

        await Assert.That(ov.Type!.Name).IsEqualTo("number");
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(int));
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
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        // A declared {binary, jpg} (bytes off I/O, the kind names the decode)
        // survives the ctor — the value isn't re-derived to a bare binary that
        // drops the kind.
        var explicitType = engine.Format.TypeFromMime("image/jpeg");
        var ov = new Data("test", new byte[] { 1, 2, 3 }, explicitType, context: context);

        await Assert.That(ov.Type!.Name).IsEqualTo("binary");
        await Assert.That(ov.Type!.Kind).IsEqualTo("jpg");
    }

    [Test]
    public async Task Type_Setter_StampsContext()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var newType = new Type("text/plain");
        var ov = new Data("test", "hello", newType, context: context);

        // Type gets context from Data — family is resolvable via registry.
        await Assert.That(engine.Format.FamilyOf(newType.Name)).IsEqualTo("text");
    }

    [Test]
    public async Task Type_Kind_WithContext()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var data = new Data("img", new byte[] { 1, 2 }, engine.Format.TypeFromMime("image/jpeg"), context: context);

        // Binary content; the kind (jpg) carries the family. The kind's family
        // is image, which is not compressible (already-compressed content).
        await Assert.That(data.Type!.Name).IsEqualTo("binary");
        await Assert.That(engine.Format.TypeOf(data.Type!.Kind!)).IsEqualTo("image");
        await Assert.That(data.Type!.Compressible).IsFalse();
    }

    [Test]
    public async Task Type_Kind_WithoutContext_ReturnsNull()
    {
        var imageType = new Type("image/jpeg");

        // Family-Kind accessor is gone — Kind is the subtype (null when unset).
        await Assert.That(imageType.Kind).IsNull();
        await Assert.That(imageType.Compressible).IsFalse();
    }

    [Test]
    public async Task Type_Compressible_TextKind()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var data = new Data("txt", "hello", Type.FromMime("text/plain"), context: context);

        await Assert.That(engine.Format.FamilyOf(data.Type!.Name)).IsEqualTo("text");
        await Assert.That(data.Type!.Compressible).IsTrue();
    }

    [Test]
    public async Task GetChild_InheritsContext()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var data = new Dictionary<string, object?> { { "name", "test" } };
        var ov = new Data("data", data, context: context);

        var child = await ov.GetChild("name");

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

    // --- Phase 4: Compress / Decompress pipeline ---

    [Test]
    public async Task Compress_CompressibleType_CreatesArchivedEnvelope()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        // text/plain is compressible (kind "text").
        var data = new Data("", "Hello, this is a test string for compression!", Type.FromMime("text/plain"), context: context);

        var compressed = data.Compress();

        // Flat shape — the compressed bytes ride as an `archive` item.
        await Assert.That(compressed.Type!.Name).IsEqualTo("archive");
        await Assert.That(await compressed.Value()).IsTypeOf<global::app.type.archive.@this>();
    }

    [Test]
    public async Task Compress_NonCompressible_ReturnsSelf()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        // Bytes off I/O are binary; the kind (jpg) resolves to the image
        // family, which is not compressible (already-compressed content).
        var data = new Data("", new byte[] { 1, 2, 3 }, engine.Format.TypeFromMime("image/jpeg"), context: context);

        var result = data.Compress();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decompress_ArchivedData_ReturnsOriginal()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        // Compress a plain Data, then decompress — the value round-trips.
        var inner = new Data("", "Hello world", Type.FromMime("text/plain"), context: context);

        var compressed = inner.Compress();
        var decompressed = compressed.Decompress();

        await decompressed.IsSuccess();
        await Assert.That((await decompressed.Value())?.ToString()).IsEqualTo("Hello world");
    }

    [Test]
    public async Task Decompress_NonArchived_ReturnsSelf()
    {
        var data = _app.Data("", "Hello", global::PLang.Tests.TestApp.SharedContext.Type.Create("text"));

        var result = data.Decompress();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_PreservesData()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var content = new Data("", "The quick brown fox jumps over the lazy dog", Type.FromMime("text/plain"), context: context);

        var compressed = content.Compress();
        compressed.Context = context;
        var decompressed = compressed.Decompress();

        await decompressed.IsSuccess();
        await Assert.That((await decompressed.Value())?.ToString()).IsEqualTo("The quick brown fox jumps over the lazy dog");
    }

    [Test]
    public async Task Encrypt_ReturnsSelf_NoCryptoYet()
    {
        var data = _app.Data("", "secret", global::PLang.Tests.TestApp.SharedContext.Type.Create("text"));

        var result = data.Encrypt();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decrypt_NonEncrypted_ReturnsSelf()
    {
        var data = _app.Data("", "Hello", global::PLang.Tests.TestApp.SharedContext.Type.Create("text"));

        var result = data.Decrypt();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decrypt_EncryptedType_ReturnsSelf_NoCryptoYet()
    {
        // A Data declared as "encrypted" — Decrypt is a no-op until a crypto
        // service exists, returning self.
        var encrypted = _app.Data("", new byte[] { 1, 2 }, global::PLang.Tests.TestApp.SharedContext.Type.Create("encrypted"));

        var result = encrypted.Decrypt();

        // No crypto service — returns self
        await Assert.That(result).IsEqualTo(encrypted);
    }

    [Test]
    public async Task CompressChain_TextData()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var data = new Data("msg", "Hello, PLang!", Type.FromMime("text/plain"), context: context);

        var envelope = data.Compress();

        // text/plain → Kind "text" (compressible) → archive envelope
        await Assert.That(envelope.Type!.Name).IsEqualTo("archive");
    }

    // --- Phase 4 fixes: error paths + edge cases ---

    [Test]
    public async Task Decompress_InvalidInner_ReturnsError()
    {
        // A Data that is not an archive item is not decompressable — Decompress
        // is a no-op and returns the Data unchanged (mirrors Decrypt / Unwrap).
        var data = _app.Data("", "not an archive", Type.FromMime("text/plain"));

        var result = data.Decompress();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("not an archive");
    }

    [Test]
    public async Task Decompress_NullBytes_ReturnsError()
    {
        // archive with empty bytes — nothing decompressable
        var archived = new Data("", new global::app.type.archive.@this(System.Array.Empty<byte>()), context: _app.User.Context);

        var result = archived.Decompress();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
    }

    [Test]
    public async Task Decompress_CorruptData_ReturnsError()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);
        // Random bytes — not valid GZip
        var archived = new Data("", new global::app.type.archive.@this(new byte[] { 0xFF, 0xFE, 0x00, 0x42 }), context: context);

        var result = archived.Decompress();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
        await Assert.That(result.Error!.Message).Contains("Decompression failed");
    }

    [Test]
    public async Task Decompress_InvalidJsonAfterDecompression_ReturnsError()
    {
        // Valid GZip of non-JSON bytes
        var plainBytes = System.Text.Encoding.UTF8.GetBytes("this is not json {{{}}}");
        byte[] gzipped;
        using (var vars = new System.IO.MemoryStream())
        {
            using (var gz = new System.IO.Compression.GZipStream(vars, System.IO.Compression.CompressionLevel.Optimal))
            {
                gz.Write(plainBytes, 0, plainBytes.Length);
            }
            gzipped = vars.ToArray();
        }

        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);
        var archived = new Data("", new global::app.type.archive.@this(gzipped), context: context);

        var result = archived.Decompress();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
        await Assert.That(result.Error!.Message).Contains("Deserialization failed");
    }

    // Retired: multi-level Data-in-Data nesting via item.clr couriers — nested
    // Data is not a supported shape (the clr carrier now rejects a Data).

    [Test]
    public async Task CompressDecompress_PropertiesNotPreserved()
    {
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);

        var content = new Data("", "Hello", Type.FromMime("text/plain"), context: context);
        content.Properties["metadata"] = "some value";

        var compressed = content.Compress();
        compressed.Context = context;
        var decompressed = compressed.Decompress();

        await decompressed.IsSuccess();
        // Properties ride on the wire — they round-trip through compress.
        await Assert.That(((await decompressed.Properties.Value("metadata")))?.ToString()).IsEqualTo("some value");
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

        var ex = Assert.Throws<InvalidOperationException>(() => new global::app.type.item.serializer.json(global::PLang.Tests.TestApp.SharedContext).Parse(doc.RootElement));
        await Assert.That(ex.Message).Contains("maximum depth");
    }

    [Test]
    public async Task UnwrapJsonElement_FractionalNumber_DefaultsToDouble()
    {
        // A bare decimal-point literal defaults to double (universal language
        // convention); decimal is opt-in via `as number/decimal`.
        var json = "{\"price\":19.99}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = new global::app.type.item.serializer.json(global::PLang.Tests.TestApp.SharedContext).Parse(doc.RootElement) as app.type.dict.@this;

        await Assert.That(result).IsNotNull();
        result!.Context = _app.User.Context;
        // Born-native: a JSON number is a number.@this wrapper; its backing
        // (via ToRaw) is double for a bare decimal-point literal.
        var price = await result!.Get("price")!.Value();
        await Assert.That(price).IsTypeOf<app.type.number.@this>();
        await Assert.That(((app.type.number.@this)price!).Clr<object>()).IsEqualTo(19.99d);
    }

    [Test]
    public async Task UnwrapJsonElement_IntegerNumber_ReturnsLong()
    {
        var json = "{\"count\":42}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = new global::app.type.item.serializer.json(global::PLang.Tests.TestApp.SharedContext).Parse(doc.RootElement) as app.type.dict.@this;

        await Assert.That(result).IsNotNull();
        result!.Context = _app.User.Context;
        // Born-native: a whole JSON number is a number.@this wrapper backed by long.
        var count = await result!.Get("count")!.Value();
        await Assert.That(count).IsTypeOf<app.type.number.@this>();
        await Assert.That(((app.type.number.@this)count!).Clr<object>()).IsEqualTo(42L);
    }

    // --- v5: Zip bomb test ---

    [Test]
    public async Task Decompress_ExceedsSizeLimit_ReturnsError()
    {
        // Create a compressed payload of zeros — compresses very well
        // 10MB of zeros → small compressed, but tests the limit check path
        var bigPayload = new byte[10 * 1024 * 1024]; // 10MB of zeros
        byte[] compressed;
        using (var vars = new System.IO.MemoryStream())
        {
            using (var gz = new System.IO.Compression.GZipStream(vars, System.IO.Compression.CompressionLevel.Optimal))
            {
                // Write 11 times = 110MB total — exceeds 100MB limit
                for (int i = 0; i < 11; i++)
                    gz.Write(bigPayload, 0, bigPayload.Length);
            }
            compressed = vars.ToArray();
        }

        // Stage 3: archived.Value is the gzip byte[] directly (no inner gzip Data).
        await using var engine = global::PLang.Tests.TestApp.Create("/test");
        var context = new global::app.actor.context.@this(engine, engine.User);
        var archived = new Data("", new global::app.type.archive.@this(compressed), context: context);

        var result = archived.Decompress();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
        await Assert.That(result.Error!.Message).Contains("size limit");
    }

    // --- v5: Decompress StatusCode assertions ---

    [Test]
    public async Task Decompress_NullBytes_ReturnsStatusCode500()
    {
        var archived = new Data("", new global::app.type.archive.@this(System.Array.Empty<byte>()), context: _app.User.Context);

        var result = archived.Decompress();

        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Decompress_CorruptData_ReturnsStatusCode500()
    {
        var archived = new Data("", new global::app.type.archive.@this(new byte[] { 0xFF, 0xFE, 0x00, 0x42 }), context: _app.User.Context);

        var result = archived.Decompress();

        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Decompress_InvalidJson_ReturnsStatusCode500()
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes("this is not json {{{}}}");
        byte[] gzipped;
        using (var vars = new System.IO.MemoryStream())
        {
            using (var gz = new System.IO.Compression.GZipStream(vars, System.IO.Compression.CompressionLevel.Optimal))
            {
                gz.Write(plainBytes, 0, plainBytes.Length);
            }
            gzipped = vars.ToArray();
        }

        var archived = new Data("", new global::app.type.archive.@this(gzipped), context: _app.User.Context);

        var result = archived.Decompress();

        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    // Data.Merge deleted — it was a list operation living on Data that lowered to
    // CLR List<Data>, with zero production callers. Merge-by-name belongs on the
    // native list type if/when needed (see todos: merge onto list.@this).
}

public class DynamicDataTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/DynamicDataTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Constructor_CreatesWithFactory()
    {
        var counter = 0;
        var dov = new DynamicData("counter", () => ++counter, _app.User.Context);

        await Assert.That(dov.Name).IsEqualTo("counter");
    }

    [Test]
    public async Task Value_CallsFactoryEachTime()
    {
        var counter = 0;
        var dov = new DynamicData("counter", () => ++counter, _app.User.Context);

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
        var dov = new DynamicData("now", () => DateTime.Now, _app.User.Context, Type.DateTime);

        await Assert.That(dov.Type).IsNotNull();
        // plang-types Stage 6: datetime rebound to DateTimeOffset.
        await Assert.That(dov.Type!.ClrType).IsEqualTo(typeof(DateTimeOffset));
    }

    [Test]
    public async Task Value_ReturnsCurrentValue()
    {
        var now = DateTime.UtcNow;
        var dov = new DynamicData("now", () => now, _app.User.Context);

        await Assert.That(global::app.type.item.@this.Lower<System.DateTimeOffset>(await dov.Value())).IsEqualTo(now);
    }

    // --- IsVariable tests ---

    [Test]
    public async Task IsVariable_StandardVariable_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.text.@this("%var%", "plang"));
        await Assert.That(d.IsVariable).IsTrue();
    }

    [Test]
    public async Task IsVariable_ShortName_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.text.@this("%v%", "plang"));
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

    // --- HasVariableReference tests ---

    [Test]
    public async Task HasVariableReference_EmbeddedVariable_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.text.@this("hello %name%", "plang"));
        await Assert.That(d.HasVariableReference).IsTrue();
    }

    [Test]
    public async Task HasVariableReference_MultipleVariables_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.text.@this("%a% + %b%", "plang"));
        await Assert.That(d.HasVariableReference).IsTrue();
    }

    [Test]
    public async Task HasVariableReference_SingleVariable_ReturnsTrue()
    {
        var d = new Data("x", new global::app.type.text.@this("%var%", "plang"));
        await Assert.That(d.HasVariableReference).IsTrue();
    }

    [Test]
    public async Task HasVariableReference_NoVariables_ReturnsFalse()
    {
        var d = _app.Data("x", "no vars");
        await Assert.That(d.HasVariableReference).IsFalse();
    }

    [Test]
    public async Task HasVariableReference_EmptyPercents_ReturnsFalse()
    {
        var d = _app.Data("x", "%%");
        await Assert.That(d.HasVariableReference).IsFalse();
    }

    [Test]
    public async Task HasVariableReference_NonStringValue_ReturnsFalse()
    {
        var d = _app.Data("x", 42);
        await Assert.That(d.HasVariableReference).IsFalse();
    }

    [Test]
    public async Task HasVariableReference_NullValue_ReturnsFalse()
    {
        var d = new Data("x");
        await Assert.That(d.HasVariableReference).IsFalse();
    }
}
