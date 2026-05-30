using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

// Integration cut 1 — a typed `set` round-trips its kind end-to-end.
// Build → run → variable inspection. Pins that structured type, the text type,
// extension→kind derivation, and mint-carries-kind all fire together and the
// LLM-emitted `as text` lands at the right Type.Kind on the minted variable.
// This is the regression guard for the dropped-kind bug.

public class Cut1_TypedSetRoundTripsKind
{
    [Test] public async Task SetReadmeMdAsText_DocTypeIsTextWithKindMd()
    {
        // Goal contains a single step:
        //   - set %doc% = "readme.md" as text
        // Build the goal, run it, then assert via the real Variables:
        //   - %doc% exists, %doc.Type.Name% == "text", %doc.Type.Kind% == "md"
        //   - %doc.Kind% == "md" (sourced from Type.Kind)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task SetReadmeMdAsText_NavigationResolvesKindFromVariableExpression()
    {
        // After the above goal runs, a second step:
        //   - write out %doc.Type.Name%
        // produces "text" on the channel; %doc.Type.Kind% produces "md".
        // The navigation must walk through the entity (folded; not via a separate
        // Data.Kind property reach).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
