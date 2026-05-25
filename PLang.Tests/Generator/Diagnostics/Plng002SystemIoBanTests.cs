using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.Generator.Diagnostics;

/// <summary>
/// PLNG002 — bans <c>System.IO.*</c> reaches and <c>data.@this&lt;string&gt;</c>
/// Path-typed properties under <c>PLang/app/**</c> (excluding
/// <c>PLang/app/types/path/**</c> and Generators).
///
/// Stage 1 lands the diagnostic in <b>warning</b> mode; Stage 6 flips to
/// <b>error</b>. The fires/silent fixtures below pin the rule independently
/// of severity.
///
/// Allowlist: <c>System.IO.Path.DirectorySeparatorChar</c> /
/// <c>AltDirectorySeparatorChar</c> (separator constants, not IO).
/// </summary>
public class Plng002SystemIoBanTests
{
    [Test] public async Task Fires_OnFileReadAllText_UnderModulesNamespace()
    {
        // System.IO.File.ReadAllText in PLang.app.modules.foo → PLNG002.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task Fires_OnDirectoryGetFiles_UnderModulesNamespace()
    {
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task Fires_OnSystemIoPathCombine_UnderModulesNamespace()
    {
        // System.IO.Path.Combine is the most common offender; must be flagged.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task Fires_OnDataOfString_NamedPath_InActionHandler()
    {
        // [Action] handler with `Data<string> Path { get; init; }` must trip PLNG002.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task DoesNotFire_OnSystemIoPathDirectorySeparatorChar()
    {
        // Separator constants are allowlisted — not an IO reach.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task DoesNotFire_InsidePathTypesNamespace()
    {
        // app.types.path.** legitimately uses System.IO behind AuthGate — exempt.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task DoesNotFire_OnDataOfPath_InActionHandler()
    {
        // Data<path> is the correct shape — must not trip.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task DiagnosticLocation_UnderlinesOffendingMemberAccess()
    {
        // Squiggle pins to the offending call/property — mirror PLNG001 location pin.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
