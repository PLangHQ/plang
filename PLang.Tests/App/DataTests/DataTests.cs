using System.Reflection;
using app;
using app.variable;
using Type = global::app.data.type;

namespace PLang.Tests.App.DataTests;

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
    public async Task Constructor_WithNullValue_IsInitialized()
    {
        var ov = new Data("test", null);

        await Assert.That(ov.Value).IsNull();
        await Assert.That(ov.IsInitialized).IsTrue();
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
    public async Task GetChild_NonexistentPath_ReturnsNotInitialized()
    {
        var data = new Dictionary<string, object?> { { "name", "test" } };
        var ov = new Data("data", data);

        var child = ov.GetChild("nonexistent");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetChild_OutOfBoundsIndex_ReturnsNotInitialized()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = new Data("items", data);

        var child = ov.GetChild("[10]");

        await Assert.That(child.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetChild_NegativeIndex_ReturnsNotInitialized()
    {
        var data = new List<int> { 1, 2, 3 };
        var ov = new Data("items", data);

        var child = ov.GetChild("[-1]");

        await Assert.That(child.IsInitialized).IsFalse();
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
    public async Task GetChild_NullValue_ReturnsNotInitialized()
    {
        var ov = new Data("test");

        var child = ov.GetChild("anything");

        await Assert.That(child.IsInitialized).IsFalse();
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
        await Assert.That(ov.IsInitialized).IsTrue();
    }

    [Test]
    public async Task NotFound_CreatesUninitializedData()
    {
        var ov = Data.NotFound("missing");

        await Assert.That(ov.Name).IsEqualTo("missing");
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

    // --- Phase 2: Context + Lazy Type derivation ---

    [Test]
    public async Task Context_DefaultsToNull()
    {
        var ov = new Data("test", "hello");

        await Assert.That(ov.Context).IsNull();
    }

    [Test]
    public async Task Context_WhenSet_PropagesToType()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        // Use MIME type — Kind is only non-null when context is set
        var ov = new Data("test", new byte[] { 1, 2 }, Type.FromMime("image/jpeg"));
        ov.Context = context;

        // Type has context: Kind navigation works
        await Assert.That(ov.Type!.Kind).IsEqualTo("image");
    }

    [Test]
    public async Task Type_LazyDerivation_WithoutContext()
    {
        var ov = new Data("test", 42);

        // Type is lazily derived on first access
        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.Value).IsEqualTo("int");
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Type_LazyDerivation_WithContext()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var ov = new Data("test", "hello");
        ov.Context = context;

        // Type lazily derived through context's Engine.Types
        await Assert.That(ov.Type).IsNotNull();
        await Assert.That(ov.Type!.Value).IsEqualTo("string");
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Type_LazyDerivation_InvalidatedByValueSetter()
    {
        var ov = new Data("test", "hello");

        await Assert.That(ov.Type!.Value).IsEqualTo("string");

        ov.Value = 42;

        await Assert.That(ov.Type!.Value).IsEqualTo("int");
        await Assert.That(ov.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Type_NullValue_ReturnsNull()
    {
        var ov = new Data("test");

        await Assert.That(ov.Type).IsNull();
    }

    [Test]
    public async Task Type_ExplicitType_NotOverridden()
    {
        var explicitType = new Type("image/jpeg");
        var ov = new Data("test", new byte[] { 1, 2, 3 }, explicitType);

        // Explicit type is preserved, not lazily derived
        await Assert.That(ov.Type).IsEqualTo(explicitType);
        await Assert.That(ov.Type!.Value).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task Type_Setter_StampsContext()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var ov = new Data("test", "hello");
        ov.Context = context;

        var newType = new Type("text/plain");
        ov.Type = newType;

        // Type gets context from Data — Kind navigation works
        await Assert.That(newType.Kind).IsEqualTo("text");
    }

    [Test]
    public async Task Type_Kind_WithContext()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var data = new Data("img", new byte[] { 1, 2 }, Type.FromMime("image/jpeg"));
        data.Context = context;

        await Assert.That(data.Type!.Kind).IsEqualTo("image");
        await Assert.That(data.Type!.Compressible).IsFalse();
    }

    [Test]
    public async Task Type_Kind_WithoutContext_ReturnsNull()
    {
        var imageType = new Type("image/jpeg");

        await Assert.That(imageType.Kind).IsNull();
        await Assert.That(imageType.Compressible).IsFalse();
    }

    [Test]
    public async Task Type_Compressible_TextKind()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var data = new Data("txt", "hello", Type.FromMime("text/plain"));
        data.Context = context;

        await Assert.That(data.Type!.Kind).IsEqualTo("text");
        await Assert.That(data.Type!.Compressible).IsTrue();
    }

    [Test]
    public async Task GetChild_InheritsContext()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var data = new Dictionary<string, object?> { { "name", "test" } };
        var ov = new Data("data", data);
        ov.Context = context;

        var child = ov.GetChild("name");

        await Assert.That(child.IsInitialized).IsTrue();
        await Assert.That(child.Context).IsEqualTo(context);
    }

    // --- Phase 3: Envelope properties + Out view ---

    [Test]
    public async Task Signature_DefaultsToNull()
    {
        var data = new Data("test", "hello");

        await Assert.That(data.Signature).IsNull();
    }

    [Test]
    public async Task Signature_CanBeSet()
    {
        var data = new Data("test", "hello");
        var sig = new global::app.module.signing.Signature { Type = "signature", Nonce = "test" };

        data.Signature = sig;

        await Assert.That(data.Signature).IsNotNull();
        await Assert.That(data.Signature!.Nonce).IsEqualTo("test");
    }

    [Test]
    public async Task Signature_HasOutAttribute()
    {
        var prop = typeof(Data).GetProperty(nameof(Data.Signature));

        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.GetCustomAttribute<OutAttribute>()).IsNotNull();
    }

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
    public async Task Signature_HasInAttribute()
    {
        var prop = typeof(Data).GetProperty(nameof(Data.Signature));

        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.GetCustomAttribute<InAttribute>()).IsNotNull();
    }

    [Test]
    public async Task OutView_ExistsInViewEnum()
    {
        var outValue = View.Out;

        await Assert.That(outValue.ToString()).IsEqualTo("Out");
    }

    // --- Phase 4: Envelope pipeline ---

    [Test]
    public async Task Wrap_MimeType_CreatesKindEnvelope()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var data = new Data("file", new byte[] { 1, 2, 3 }, Type.FromMime("image/jpeg"));
        data.Context = context;

        var wrapped = data.Wrap();

        await Assert.That(wrapped).IsNotEqualTo(data);
        await Assert.That(wrapped.Type!.Value).IsEqualTo("image");
        await Assert.That(wrapped.Value).IsTypeOf<Data>();
        await Assert.That(wrapped.Context).IsEqualTo(context);
        var inner = (Data)wrapped.Value!;
        await Assert.That(inner.Type!.Value).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task Wrap_PlangPrimitive_ReturnsSelf()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var data = new Data("count", 42);
        data.Context = context;

        var wrapped = data.Wrap();

        // "int" has no Kind — returns self
        await Assert.That(wrapped).IsEqualTo(data);
    }

    [Test]
    public async Task Wrap_NoContext_ReturnsSelf()
    {
        var data = new Data("file", new byte[] { 1, 2, 3 }, Type.FromMime("image/jpeg"));

        var wrapped = data.Wrap();

        await Assert.That(wrapped).IsEqualTo(data);
    }

    [Test]
    public async Task Unwrap_Envelope_ReturnsInner()
    {
        var inner = new Data("", "Hello", Type.FromMime("text/plain"));
        var envelope = new Data("", inner, Type.FromName("text"));

        var unwrapped = envelope.Unwrap();

        await Assert.That(unwrapped.Type!.Value).IsEqualTo("text/plain");
        await Assert.That(unwrapped.Value).IsEqualTo("Hello");
    }

    [Test]
    public async Task Unwrap_FlatData_ReturnsSelf()
    {
        var data = new Data("test", "Hello");

        var unwrapped = data.Unwrap();

        await Assert.That(unwrapped).IsEqualTo(data);
    }

    [Test]
    public async Task Unwrap_StampsContext()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var inner = new Data("", "Hello", Type.FromMime("text/plain"));
        var envelope = new Data("", inner, Type.FromName("text"));
        envelope.Context = context;

        var unwrapped = envelope.Unwrap();

        await Assert.That(unwrapped.Context).IsEqualTo(context);
    }

    [Test]
    public async Task Compress_CompressibleType_CreatesArchivedEnvelope()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        // Create a "text" envelope (text is compressible)
        var inner = new Data("", "Hello, this is a test string for compression!", Type.FromMime("text/plain"));
        inner.Context = context;
        var wrapped = new Data("", inner, Type.FromName("text"));
        wrapped.Context = context;

        var compressed = wrapped.Compress();

        // Stage 3: flat shape — archived.Value is byte[] directly, no nested gzip Data.
        await Assert.That(compressed.Type!.Value).IsEqualTo("archived");
        await Assert.That(compressed.Value).IsTypeOf<byte[]>();
    }

    [Test]
    public async Task Compress_NonCompressible_ReturnsSelf()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        // "image" is not compressible
        var inner = new Data("", new byte[] { 1, 2, 3 }, Type.FromMime("image/jpeg"));
        inner.Context = context;
        var wrapped = new Data("", inner, Type.FromName("image"));
        wrapped.Context = context;

        var result = wrapped.Compress();

        await Assert.That(result).IsEqualTo(wrapped);
    }

    [Test]
    public async Task Compress_NoContext_ReturnsSelf()
    {
        var data = new Data("", "Hello", Type.FromName("text"));

        var result = data.Compress();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decompress_ArchivedData_ReturnsOriginal()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        // Build a text envelope, compress it, then decompress
        var inner = new Data("", "Hello world", Type.FromMime("text/plain"));
        inner.Context = context;
        var wrapped = new Data("", inner, Type.FromName("text"));
        wrapped.Context = context;

        var compressed = wrapped.Compress();
        var decompressed = compressed.Decompress();

        await Assert.That(decompressed.Success).IsTrue();
        await Assert.That(decompressed.Type!.Value).IsEqualTo("text");
        await Assert.That(decompressed.Value).IsTypeOf<Data>();
        var decompressedInner = (Data)decompressed.Value!;
        await Assert.That(decompressedInner.Value).IsEqualTo("Hello world");
    }

    [Test]
    public async Task Decompress_NonArchived_ReturnsSelf()
    {
        var data = new Data("", "Hello", Type.FromName("text"));

        var result = data.Decompress();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_PreservesData()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var content = new Data("", "The quick brown fox jumps over the lazy dog", Type.FromMime("text/plain"));
        content.Context = context;
        var wrapped = new Data("", content, Type.FromName("text"));
        wrapped.Context = context;

        var compressed = wrapped.Compress();
        compressed.Context = context;
        var decompressed = compressed.Decompress();

        await Assert.That(decompressed.Success).IsTrue();
        await Assert.That(decompressed.Type!.Value).IsEqualTo("text");

        // Unwrap to get back to the content
        var unwrapped = decompressed.Unwrap();
        await Assert.That(unwrapped.Value).IsEqualTo("The quick brown fox jumps over the lazy dog");
    }

    [Test]
    public async Task Encrypt_ReturnsSelf_NoCryptoYet()
    {
        var data = new Data("", "secret", Type.FromName("text"));

        var result = data.Encrypt();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decrypt_NonEncrypted_ReturnsSelf()
    {
        var data = new Data("", "Hello", Type.FromName("text"));

        var result = data.Decrypt();

        await Assert.That(result).IsEqualTo(data);
    }

    [Test]
    public async Task Decrypt_EncryptedType_ReturnsSelf_NoCryptoYet()
    {
        var inner = new Data("", new byte[] { 1, 2 }, Type.FromName("ed25519"));
        var encrypted = new Data("", inner, Type.FromName("encrypted"));

        var result = encrypted.Decrypt();

        // No crypto service — returns self
        await Assert.That(result).IsEqualTo(encrypted);
    }

    [Test]
    public async Task WrapCompressChain_TextData()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var data = new Data("msg", "Hello, PLang!", Type.FromMime("text/plain"));
        data.Context = context;

        var envelope = data.Wrap().Compress();

        // text/plain → Kind "text" (compressible) → archived envelope
        await Assert.That(envelope.Type!.Value).IsEqualTo("archived");
    }

    [Test]
    public async Task FullPipeline_WrapCompressUnwrap_RoundTrip()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var original = new Data("doc", "Report content here", Type.FromMime("text/plain"));
        original.Context = context;

        // Outbound: Wrap → Compress → Encrypt (encrypt is no-op)
        var outbound = original.Wrap().Compress().Encrypt();
        outbound.Context = context;

        // Inbound: Decrypt → Decompress → Unwrap
        var inbound = outbound.Decrypt().Decompress().Unwrap();

        await Assert.That(inbound.Success).IsTrue();
        await Assert.That(inbound.Value).IsEqualTo("Report content here");
    }

    // --- Phase 4 fixes: error paths + edge cases ---

    [Test]
    public async Task Decompress_InvalidInner_ReturnsError()
    {
        // Stage 3: archived.Value is byte[] directly. A string-valued archived
        // Data is a malformed envelope.
        var data = new Data("", "not a byte array", Type.FromName("archived"));

        var result = data.Decompress();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
        await Assert.That(result.Error!.Message).Contains("byte[] value");
    }

    [Test]
    public async Task Decompress_NullBytes_ReturnsError()
    {
        // archived.Value is null — no byte[] to decompress
        var archived = new Data("", null, Type.FromName("archived"));

        var result = archived.Decompress();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
        await Assert.That(result.Error!.Message).Contains("byte[] value");
    }

    [Test]
    public async Task Decompress_CorruptData_ReturnsError()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);
        // Random bytes — not valid GZip
        var archived = new Data("", new byte[] { 0xFF, 0xFE, 0x00, 0x42 }, Type.FromName("archived"));
        archived.Context = context;

        var result = archived.Decompress();

        await Assert.That(result.Success).IsFalse();
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

        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);
        var archived = new Data("", gzipped, Type.FromName("archived"));
        archived.Context = context;

        var result = archived.Decompress();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
        await Assert.That(result.Error!.Message).Contains("Deserialization failed");
    }

    [Test]
    public async Task CompressDecompress_MultiLevelNesting_PreservesAllLevels()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        // Two-level nesting: text envelope containing another text envelope
        var leaf = new Data("", "deep content", Type.FromMime("text/plain"));
        leaf.Context = context;
        var mid = new Data("", leaf, Type.FromName("text"));
        mid.Context = context;
        var outer = new Data("", mid, Type.FromName("document"));
        outer.Context = context;

        // Compress (document is compressible) and decompress
        var compressed = outer.Compress();
        compressed.Context = context;
        var decompressed = compressed.Decompress();

        await Assert.That(decompressed.Success).IsTrue();
        await Assert.That(decompressed.Type!.Value).IsEqualTo("document");
        await Assert.That(decompressed.Value).IsTypeOf<Data>();

        var midResult = (Data)decompressed.Value!;
        await Assert.That(midResult.Type!.Value).IsEqualTo("text");
        await Assert.That(midResult.Value).IsTypeOf<Data>();

        var leafResult = (Data)midResult.Value!;
        await Assert.That(leafResult.Value).IsEqualTo("deep content");
    }

    [Test]
    public async Task CompressDecompress_PropertiesNotPreserved()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        var content = new Data("", "Hello", Type.FromMime("text/plain"));
        content.Context = context;
        var wrapped = new Data("", content, Type.FromName("text"));
        wrapped.Context = context;
        wrapped.Properties["metadata"] = "some value";

        var compressed = wrapped.Compress();
        compressed.Context = context;
        var decompressed = compressed.Decompress();

        await Assert.That(decompressed.Success).IsTrue();
        // Stage 4: Properties now ride on the wire — they round-trip.
        await Assert.That(decompressed.Properties["metadata"]).IsEqualTo("some value");
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

        var ex = Assert.Throws<InvalidOperationException>(() => Data.UnwrapJsonElement(doc.RootElement));
        await Assert.That(ex.Message).Contains("maximum depth");
    }

    [Test]
    public async Task UnwrapJsonElement_DecimalNumber_PreservesPrecision()
    {
        var json = "{\"price\":19.99}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = Data.UnwrapJsonElement(doc.RootElement) as Dictionary<string, object?>;

        await Assert.That(result).IsNotNull();
        var price = result!["price"];
        await Assert.That(price).IsTypeOf<decimal>();
        await Assert.That(price).IsEqualTo(19.99m);
    }

    [Test]
    public async Task UnwrapJsonElement_IntegerNumber_ReturnsLong()
    {
        var json = "{\"count\":42}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = Data.UnwrapJsonElement(doc.RootElement) as Dictionary<string, object?>;

        await Assert.That(result).IsNotNull();
        var count = result!["count"];
        await Assert.That(count).IsTypeOf<long>();
        await Assert.That(count).IsEqualTo(42L);
    }

    [Test]
    public async Task GetChild_DeeplyNestedPath_ReturnsDepthError()
    {
        // Build a path with 150 dot segments — exceeds MaxNavigationDepth (100)
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var current = dict;
        for (int i = 0; i < 150; i++)
        {
            var inner = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            current["a"] = inner;
            current = inner;
        }
        current["a"] = "leaf";

        var data = new Data("root", dict);
        var path = string.Join(".", Enumerable.Repeat("a", 151));

        var result = data.GetChild(path);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NavigationDepthExceeded");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
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
        await using var engine = new global::app.@this("/test");
        var ctx = new global::app.actor.context.@this(engine);
        var archived = new Data("", compressed, Type.FromName("archived"));
        archived.Context = ctx;

        var result = archived.Decompress();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
        await Assert.That(result.Error!.Message).Contains("size limit");
    }

    // --- v5: Decompress StatusCode assertions ---

    [Test]
    public async Task Decompress_InvalidInner_ReturnsStatusCode500()
    {
        var data = new Data("", "not a Data object", Type.FromName("archived"));

        var result = data.Decompress();

        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Decompress_NullBytes_ReturnsStatusCode500()
    {
        var inner = new Data("", null, Type.FromName("gzip"));
        var archived = new Data("", inner, Type.FromName("archived"));

        var result = archived.Decompress();

        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Decompress_CorruptData_ReturnsStatusCode500()
    {
        var inner = new Data("", new byte[] { 0xFF, 0xFE, 0x00, 0x42 }, Type.FromName("gzip"));
        var archived = new Data("", inner, Type.FromName("archived"));

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

        var inner = new Data("", gzipped, Type.FromName("gzip"));
        var archived = new Data("", inner, Type.FromName("archived"));

        var result = archived.Decompress();

        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    // --- v5: Merge tests ---

    [Test]
    public async Task Merge_TwoLists_CombinesByName()
    {
        var list1 = new List<Data> { new Data("x", 1), new Data("y", 2) };
        var list2 = new List<Data> { new Data("y", 99), new Data("z", 3) };

        var a = new Data("") { Value = list1 };
        var b = new Data("") { Value = list2 };

        var result = a.Merge(b);
        var merged = result.Value as List<Data>;

        await Assert.That(merged).IsNotNull();
        await Assert.That(merged!.Count).IsEqualTo(3); // x, y (replaced), z
        await Assert.That(merged[0].Value).IsEqualTo(1); // x unchanged
        await Assert.That(merged[1].Value).IsEqualTo(99); // y replaced
        await Assert.That(merged[2].Value).IsEqualTo(3); // z appended
    }

    [Test]
    public async Task Merge_NullOtherValue_ReturnsSelf()
    {
        var list = new List<Data> { new Data("x", 1) };
        var a = new Data("") { Value = list };
        var b = new Data("", null);

        var result = a.Merge(b);

        await Assert.That(result).IsSameReferenceAs(a);
    }

    [Test]
    public async Task Merge_NonListValues_ReturnsEmptyList()
    {
        // Current behavior: non-List values cast to null → empty list, other items added
        var a = new Data("", "not a list");
        var b = new Data("", "also not a list");

        var result = a.Merge(b);
        var merged = result.Value as List<Data>;

        // Both cast as List<Data> produce empty lists — result is empty
        await Assert.That(merged).IsNotNull();
        await Assert.That(merged!.Count).IsEqualTo(0);
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
        // plang-types Stage 6: datetime rebound to DateTimeOffset.
        await Assert.That(dov.Type!.ClrType).IsEqualTo(typeof(DateTimeOffset));
    }

    [Test]
    public async Task Value_ReturnsCurrentValue()
    {
        var now = DateTime.UtcNow;
        var dov = new DynamicData("now", () => now);

        await Assert.That(dov.Value).IsEqualTo(now);
    }

    // --- IsVariable tests ---

    [Test]
    public async Task IsVariable_StandardVariable_ReturnsTrue()
    {
        var d = new Data("x", "%var%");
        await Assert.That(d.IsVariable).IsTrue();
    }

    [Test]
    public async Task IsVariable_ShortName_ReturnsTrue()
    {
        var d = new Data("x", "%v%");
        await Assert.That(d.IsVariable).IsTrue();
    }

    [Test]
    public async Task IsVariable_EmptyPercents_ReturnsFalse()
    {
        var d = new Data("x", "%%");
        await Assert.That(d.IsVariable).IsFalse();
    }

    [Test]
    public async Task IsVariable_EmbeddedVariable_ReturnsFalse()
    {
        var d = new Data("x", "hello %var%");
        await Assert.That(d.IsVariable).IsFalse();
    }

    [Test]
    public async Task IsVariable_VariableWithTrailing_ReturnsFalse()
    {
        var d = new Data("x", "%var% + 1");
        await Assert.That(d.IsVariable).IsFalse();
    }

    [Test]
    public async Task IsVariable_NonStringValue_ReturnsFalse()
    {
        var d = new Data("x", 42);
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
        var d = new Data("x", "hello %name%");
        await Assert.That(d.HasVariableReference).IsTrue();
    }

    [Test]
    public async Task HasVariableReference_MultipleVariables_ReturnsTrue()
    {
        var d = new Data("x", "%a% + %b%");
        await Assert.That(d.HasVariableReference).IsTrue();
    }

    [Test]
    public async Task HasVariableReference_SingleVariable_ReturnsTrue()
    {
        var d = new Data("x", "%var%");
        await Assert.That(d.HasVariableReference).IsTrue();
    }

    [Test]
    public async Task HasVariableReference_NoVariables_ReturnsFalse()
    {
        var d = new Data("x", "no vars");
        await Assert.That(d.HasVariableReference).IsFalse();
    }

    [Test]
    public async Task HasVariableReference_EmptyPercents_ReturnsFalse()
    {
        var d = new Data("x", "%%");
        await Assert.That(d.HasVariableReference).IsFalse();
    }

    [Test]
    public async Task HasVariableReference_NonStringValue_ReturnsFalse()
    {
        var d = new Data("x", 42);
        await Assert.That(d.HasVariableReference).IsFalse();
    }

    [Test]
    public async Task HasVariableReference_NullValue_ReturnsFalse()
    {
        var d = new Data("x");
        await Assert.That(d.HasVariableReference).IsFalse();
    }
}
