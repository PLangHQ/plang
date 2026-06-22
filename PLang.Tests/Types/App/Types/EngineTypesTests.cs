namespace PLang.Tests.App.Types;

public class EngineTypesTests
{
    private global::app.type.catalog.@this _types = null!;
    private global::app.format.list.@this _formats = null!;

    [Before(Test)]
    public void Setup()
    {
        _types = new global::app.type.catalog.@this();
        _formats = new global::app.format.list.@this();
    }

    // --- Type/format mapping tables (data-driven; a failing row names itself) ---

    [Test]
    public async Task Clr_MapsNamesAndMimesToClrTypes()
    {
        (string name, System.Type? expected)[] table =
        {
            ("string", typeof(string)), ("text", typeof(string)),
            ("STRING", typeof(string)), ("StRiNg", typeof(string)),   // case-insensitive
            ("int", typeof(int)), ("long", typeof(long)), ("bool", typeof(bool)),
            ("datetime", typeof(DateTimeOffset)), ("bytes", typeof(byte[])),
            ("int?", typeof(int?)), ("guid?", typeof(Guid?)),
            ("list<string>", typeof(List<string>)), ("list<int>", typeof(List<int>)),
            ("dict<string,int>", typeof(Dictionary<string, int>)),
            ("dictionary<string,int>", typeof(Dictionary<string, int>)),
            ("text/plain", typeof(string)), ("image/jpeg", typeof(byte[])),
            ("application/json", typeof(object)), ("application/octet-stream", typeof(byte[])),
            ("unknowntype", null), (null!, null), ("", null), ("   ", null),
        };
        foreach (var (name, expected) in table)
        {
            var actual = _types.Clr(name);
            if (expected is null) await Assert.That(actual).IsNull().Because($"Clr(\"{name}\")");
            else await Assert.That(actual).IsEqualTo(expected).Because($"Clr(\"{name}\")");
        }
    }

    [Test]
    public async Task Name_MapsClrTypesToPlangNames()
    {
        (System.Type type, string expected)[] table =
        {
            (typeof(string), "text"), (typeof(int), "number"), (typeof(byte[]), "bytes"),
            (typeof(int?), "number?"),
            (typeof(List<string>), "list<text>"), (typeof(IList<int>), "list<number>"),
            (typeof(Dictionary<string, int>), "dict<text,number>"),
            (typeof(int[]), "list<number>"),
            (typeof(HashSet<string>), "list<text>"),        // Set/HashSet normalize to list<T>
            (typeof(SortedSet<int>), "sortedset"),          // not in map -> strip arity suffix
            (typeof(Uri), "uri"),                            // unknown -> lowercase name
        };
        foreach (var (type, expected) in table)
            await Assert.That(_types.Name(type)).IsEqualTo(expected).Because($"Name({type.Name})");

        await Assert.That(_types.Name(null!)).IsEqualTo("object").Because("Name(null)");
    }

    [Test]
    public async Task Kind_MapsExtensionsToKinds()
    {
        (string ext, string? expected)[] table =
        {
            (".jpg", "image"), (".xlsx", "spreadsheet"), (".mp4", "video"), (".mp3", "audio"),
            (".zip", "archive"), (".cs", "code"), (".pdf", "document"), (".goal", "plang"),
            ("jpg", "image"),       // without dot
            (".JPG", "image"),      // case-insensitive
            (".key", "certificate"),// conflict resolved: certificate wins over presentation
            (".xyz123", null), (null!, null), ("", null),
        };
        foreach (var (ext, expected) in table)
        {
            var actual = _formats.Kind(ext);
            if (expected is null) await Assert.That(actual).IsNull().Because($"Kind(\"{ext}\")");
            else await Assert.That(actual).IsEqualTo(expected).Because($"Kind(\"{ext}\")");
        }
    }

    [Test]
    public async Task Mime_MapsExtensionsToContentTypes()
    {
        (string ext, string expected)[] table =
        {
            (".jpg", "image/jpeg"), (".json", "application/json"),
            (".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            ("jpg", "image/jpeg"),                            // without dot
            (".xyz123", "application/octet-stream"),          // unknown -> octet-stream
            (null!, "application/octet-stream"), ("", "application/octet-stream"),
        };
        foreach (var (ext, expected) in table)
            await Assert.That(_formats.Mime(ext)).IsEqualTo(expected).Because($"Mime(\"{ext}\")");
    }

    [Test]
    public async Task Compressible_ClassifiesKinds()
    {
        (string? kind, bool expected)[] table =
        {
            ("text", true), ("spreadsheet", true), ("code", true), ("document", true),
            ("image", false), ("video", false), ("audio", false), ("archive", false),
            (null!, false), ("", false),
        };
        foreach (var (kind, expected) in table)
            await Assert.That(_formats.Compressible(kind!)).IsEqualTo(expected).Because($"Compressible(\"{kind}\")");
    }






























































    // --- Add/Remove: runtime extensibility ---

    [Test]
    public async Task Add_NewExtension_IsAccessible()
    {
        _formats.Add(".plx", "plang-extension", "application/x-plang");

        await Assert.That(_formats.Kind(".plx")).IsEqualTo("plang-extension");
        await Assert.That(_formats.Mime(".plx")).IsEqualTo("application/x-plang");
    }

    [Test]
    public async Task Add_OverrideExisting_ReplacesPrevious()
    {
        _formats.Add(".txt", "custom-text", "text/x-custom");

        await Assert.That(_formats.Kind(".txt")).IsEqualTo("custom-text");
        await Assert.That(_formats.Mime(".txt")).IsEqualTo("text/x-custom");
    }

    [Test]
    public async Task Add_MimeOptional_OnlyUpdatesKind()
    {
        var originalMime = _formats.Mime(".txt");

        _formats.Add(".txt", "custom-text");

        await Assert.That(_formats.Kind(".txt")).IsEqualTo("custom-text");
        await Assert.That(_formats.Mime(".txt")).IsEqualTo(originalMime);
    }

    [Test]
    public async Task Remove_ExistingExtension_RemovesBothKindAndMime()
    {
        _formats.Remove(".jpg");

        await Assert.That(_formats.Kind(".jpg")).IsNull();
        await Assert.That(_formats.Mime(".jpg")).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Remove_NonexistentExtension_NoError()
    {
        _formats.Remove(".doesnotexist");
        // No exception thrown
    }

    // --- KindOf: type value → kind ---

    [Test]
    public async Task KindOf_KnownKindName_ReturnsSelf()
    {
        await Assert.That(_formats.FamilyOf("image")).IsEqualTo("image");
        await Assert.That(_formats.FamilyOf("video")).IsEqualTo("video");
        await Assert.That(_formats.FamilyOf("text")).IsEqualTo("text");
        await Assert.That(_formats.FamilyOf("archive")).IsEqualTo("archive");
        await Assert.That(_formats.FamilyOf("code")).IsEqualTo("code");
    }

    [Test]
    public async Task KindOf_KnownKindName_CaseInsensitive()
    {
        await Assert.That(_formats.FamilyOf("IMAGE")).IsEqualTo("image");
        await Assert.That(_formats.FamilyOf("Video")).IsEqualTo("video");
    }

    [Test]
    public async Task KindOf_MimeType_ReturnsKind()
    {
        await Assert.That(_formats.FamilyOf("image/jpeg")).IsEqualTo("image");
        await Assert.That(_formats.FamilyOf("video/mp4")).IsEqualTo("video");
        await Assert.That(_formats.FamilyOf("audio/mpeg")).IsEqualTo("audio");
        await Assert.That(_formats.FamilyOf("text/plain")).IsEqualTo("text");
        await Assert.That(_formats.FamilyOf("application/json")).IsEqualTo("text");
        await Assert.That(_formats.FamilyOf("application/pdf")).IsEqualTo("document");
    }

    [Test]
    public async Task KindOf_PlangTypeName_ReturnsNull()
    {
        await Assert.That(_formats.FamilyOf("string")).IsNull();
        await Assert.That(_formats.FamilyOf("int")).IsNull();
        await Assert.That(_formats.FamilyOf("datetime")).IsNull();
        await Assert.That(_formats.FamilyOf("bool")).IsNull();
    }

    [Test]
    public async Task KindOf_UnknownMime_ReturnsNull()
    {
        await Assert.That(_formats.FamilyOf("application/x-unknown-test")).IsNull();
    }

    [Test]
    public async Task KindOf_NullOrEmpty_ReturnsNull()
    {
        await Assert.That(_formats.FamilyOf(null!)).IsNull();
        await Assert.That(_formats.FamilyOf("")).IsNull();
    }

    // --- Finding #1: Add() must update _allKinds/_mimeToKind for KindOf ---

    [Test]
    public async Task Add_NewExtension_KindOfFindsNewKind()
    {
        _formats.Add(".custom", "custom-kind", "application/custom");

        await Assert.That(_formats.FamilyOf("custom-kind")).IsEqualTo("custom-kind");
    }

    [Test]
    public async Task Add_NewExtension_KindOfFindsByMime()
    {
        _formats.Add(".custom", "custom-kind", "application/custom");

        await Assert.That(_formats.FamilyOf("application/custom")).IsEqualTo("custom-kind");
    }

    [Test]
    public async Task Remove_Extension_KindOfNoLongerFindsKind_WhenLastOfItsKind()
    {
        _formats.Add(".custom", "unique-kind", "application/unique");

        _formats.Remove(".custom");

        await Assert.That(_formats.FamilyOf("unique-kind")).IsNull();
        await Assert.That(_formats.FamilyOf("application/unique")).IsNull();
    }

    [Test]
    public async Task Remove_Extension_KindOfStillFindsKind_WhenOtherExtensionSharesKind()
    {
        // .jpg and .jpeg both map to "image" — removing .jpg should NOT remove "image" from _allKinds
        _formats.Remove(".jpg");

        await Assert.That(_formats.FamilyOf("image")).IsEqualTo("image");
    }

    // --- Finding #2: Kind(null)/Mime(null) null guard ---





    // --- Finding #3: Name() backtick fix for generics ---



    // --- Finding #4: BuilderNames/ComplexSchemas tests ---

    [Test]
    public async Task BuilderNames_ReturnsNonEmptyList()
    {
        var names = _types.BuilderNames();

        await Assert.That(names).IsNotNull();
        await Assert.That(names.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BuilderNames_ContainsCommonTypes()
    {
        var names = _types.BuilderNames();

        await Assert.That(names).Contains("text");
        await Assert.That(names).Contains("number");
        await Assert.That(names).Contains("bool");
        await Assert.That(names).Contains("datetime");
    }

    [Test]
    public async Task BuilderNames_ExcludesNullableVariants()
    {
        var names = _types.BuilderNames();

        await Assert.That(names).DoesNotContain("int?");
        await Assert.That(names).DoesNotContain("bool?");
    }

    [Test]
    public async Task BuilderNames_ExcludesDuplicateClrTypes()
    {
        var names = _types.BuilderNames();

        // "string" and "text" both map to typeof(string) — only the first should appear
        var stringCount = names.Count(n => n == "string" || n == "text");
        await Assert.That(stringCount).IsEqualTo(1);
    }

    [Test]
    public async Task ComplexSchemas_ReturnsDict()
    {
        var schemas = _types.ComplexSchemas();

        await Assert.That(schemas).IsNotNull();
    }

    [Test]
    public async Task ComplexSchemas_GoalCallHasSchema()
    {
        var schemas = _types.ComplexSchemas();

        // goal.call maps to GoalCall which has [LlmBuilder] properties
        await Assert.That(schemas.ContainsKey("goal.call")).IsTrue();
    }

    // --- Finding #7: Lazy derivation distinguishes context path from fallback ---

    [Test]
    public async Task Add_CustomType_LazyDerivationUsesEngineTypes()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        // Add a custom type mapping that static TypeMapping does NOT have
        engine.Format.Add(".custom", "custom-kind", "application/custom");

        var data = new global::app.data.@this("test", new byte[] { 1 },
            engine.Format.TypeFromMime("application/custom"));
        data.Context = context;

        // Bytes off I/O are binary; the kind names the subtype. The custom mime
        // only resolves to its family through the ENGINE'S registry (the runtime
        // Add), which the static TypeMapping lacks — proving lazy derivation
        // walks the engine types, not the static map.
        await Assert.That(data.Type!.Name).IsEqualTo("binary");
        await Assert.That(engine.Format.TypeOf(data.Type!.Kind!)).IsEqualTo("custom-kind");
    }

    // --- Engine integration ---

    [Test]
    public async Task Engine_HasTypesProperty()
    {
        await using var engine = new global::app.@this("/test");

        await Assert.That(engine.Type).IsNotNull();
        await Assert.That(engine.Type.Clr("string")).IsEqualTo(typeof(string));
    }

    // --- v5: Depth limit ---

    [Test]
    public async Task Clr_DeeplyNestedGeneric_ReturnsNull()
    {
        // Build list<list<list<...>>> nested 25 times — exceeds MaxGenericDepth (20)
        var typeName = "string";
        for (int i = 0; i < 25; i++)
            typeName = $"list<{typeName}>";

        var result = _types.Clr(typeName);

        // Should return null (depth exceeded), not throw
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Clr_ExactlyAtMaxDepth_Resolves()
    {
        // Build list<list<...list<string>...>> nested exactly 20 times
        // MaxGenericDepth=20, depth starts at 0, so 20 nestings reaches depth=20 (allowed)
        var typeName = "string";
        for (int i = 0; i < 20; i++)
            typeName = $"list<{typeName}>";

        var result = _types.Clr(typeName);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Clr_OneOverMaxDepth_ReturnsNull()
    {
        // Build list<list<...list<string>...>> nested exactly 21 times
        // At depth=21, check (21 > 20) is true → returns null
        var typeName = "string";
        for (int i = 0; i < 21; i++)
            typeName = $"list<{typeName}>";

        var result = _types.Clr(typeName);

        await Assert.That(result).IsNull();
    }
}
