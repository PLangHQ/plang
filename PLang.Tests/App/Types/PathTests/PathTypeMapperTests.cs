using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 2 — PLang <c>path</c> type-mapper dispatches through the scheme registry. A step
/// parameter typed <c>path</c> with a raw string value resolves into the correct
/// <c>Path</c> subclass at parameter-resolution time. The conversion lives in
/// <c>app.types.Conversion</c> and calls <c>context.App.Types.Scheme.From(raw)</c> rather
/// than constructing a <c>path</c> directly.
///
/// Polymorphism is invisible above C#: there remains ONE PLang <c>path</c> type. The
/// builder and the LLM never choose between <c>path</c>/<c>url</c>/<c>s3-path</c>.
///
/// Exercise the conversion through the same surface a real handler parameter uses — a
/// <c>data.@this&lt;Path&gt;</c> resolved from a raw value — not by calling the registry
/// directly (that is <see cref="SchemeRegistryTests"/>'s job).
/// </summary>
public class PathTypeMapperTests
{
    /// <summary>Intent: a <c>path</c>-typed value of <c>file:///abs/x.txt</c> resolves to
    /// a <c>FilePath</c> instance — the type-mapper routed it through the registry.</summary>
    [Test] public async Task PathParameter_SchemedFileValue_ResolvesTo_FilePath()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a <c>path</c>-typed value with a bare path (<c>/abs/x.txt</c>,
    /// <c>./rel.txt</c>) resolves to a <c>FilePath</c> — no scheme prefix defaults to
    /// file.</summary>
    [Test] public async Task PathParameter_BareValue_ResolvesTo_FilePath()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: the unknown-scheme typed exception thrown by
    /// <c>Scheme.From</c> is caught at the type-mapper boundary and shaped into a
    /// <c>data.@this.Fail</c> with a "scheme not registered" message — the exception does
    /// NOT escape past the type-mapper. Pins Data-flow uniformity: conversion failures are
    /// return values, not throws.</summary>
    [Test] public async Task PathParameter_UnknownScheme_BecomesDataFail_NoExceptionEscape()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a relative <c>path</c> value resolves against the goal's directory
    /// — today's <c>path.Resolve</c> behaviour is preserved through the registry route.
    /// (<c>FilePath</c> owns the relative-resolution logic that <c>path.Resolve</c> holds
    /// today.)</summary>
    [Test] public async Task PathParameter_RelativeValue_ResolvesAgainstGoalDirectory()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a <c>file.read</c> step whose <c>Path</c> parameter is a bare
    /// string still runs end-to-end after the registry rewire — the handler receives an
    /// already-correct <c>data.@this&lt;Path&gt;</c> subclass and never sees the dispatch.
    /// Run a goal with one <c>file.read</c> step and assert the read succeeds.</summary>
    [Test] public async Task FileReadStep_StringPathParameter_StillRuns_AfterRegistryRewire()
    {
        Assert.Fail("Not implemented");
    }
}
