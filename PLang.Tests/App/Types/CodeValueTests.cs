namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// app/types/code/this.cs — Source, Language, IBooleanResolvable = source non-empty.
// Kind is the language ("csharp"/"python"/…); text fallback when language not detected.

public class CodeValueTests
{
    [Test] public async Task Code_FromSourceAndLanguage_StoresBoth()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_Resolve_String_DetectsLanguageOrDefaultsToText()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_Build_RecognizedSnippet_ReturnsLanguageKind()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_Build_UnrecognizedSnippet_ReturnsText()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_IBooleanResolvable_NonEmptySource_Truthy()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_IBooleanResolvable_EmptySource_Falsy()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_PlangTypeAttribute_Registered()
        => throw new global::System.NotImplementedException();
}
