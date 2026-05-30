using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.LlmRepresentationTests;

// `app.builder.type.@this.TypeSchemas` renders the type vocabulary the LLM sees.
// Two render modes for kinds: advertised (a static Kinds list, e.g. number) and
// extension-derived (a Build hook, no Kinds, e.g. text/image).
// Records and enums still render as before.

public class TypeSchemasRendererTests
{
    [Test] public async Task Render_AdvertisedKinds_NumberRendersWithPipeList()
    {
        // The rendered block contains `number — kinds: int | long | decimal | double`
        // (or close equivalent; exact format pinned by Render_RecordType regression
        // baseline). The pipe-separated list reads as a closed set.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Render_ExtensionDerivedKinds_TextRendersWithExtensionTeaching()
    {
        // `text — kind = extension (...)` — teaches the LLM that the kind comes
        // from the file extension, with a few examples (md, txt, html).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Render_ExtensionDerivedKinds_ImageRendersWithExtensionTeaching()
    {
        // `image — kind = extension (gif, jpg, png, ...)`. Same shape as text.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Render_RecordType_StillRendersFieldsAsBeforeRefactor()
    {
        // A record type (e.g. `llmmessage`) still renders as `name: { k: T, ... }`.
        // Back-compat: no churn to record rendering.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Render_EnumType_StillRendersValuesAsBeforeRefactor()
    {
        // An enum type still renders as `name: v1 | v2 | ...`. Back-compat.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Render_TypeEntry_AsConstructor()
    {
        // The `type` entry itself renders as `type(name, kind?, strict?)` — the
        // constructor signature teaches the LLM to emit a dict with those three
        // keys, not a slash string. Replaces the `as text` prose that used to
        // live in variable.set's Notes.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
