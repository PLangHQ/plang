using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Modules.Settings;

/// <summary>
/// Stage 5 — Batch 9. <c>settings/Sqlite.cs</c> — D9b take-over API.
///
/// Sqlite opens the file itself; the handler must <c>Authorize(Write)</c>
/// before passing <c>.Absolute</c> to <c>sqliteOpen(...)</c>. Today's code
/// skips the gate entirely.
/// </summary>
public class SqliteAuthorizeDenialTests
{
    [Test] public async Task SqliteOpen_DataSourceOutsideRoot_DeniedAnswer_DoesNotOpenDb()
    {
        // datasource=/tmp/external.sqlite + "n" → Authorize fails, sqliteOpen never called.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task SqliteOpen_DataSourceInRoot_OpensSilently()
    {
        // In-root datasource → silent open, no Ask.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
