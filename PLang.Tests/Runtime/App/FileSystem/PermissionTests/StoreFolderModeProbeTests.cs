namespace PLang.Tests.App.FileSystem.PermissionTests;

/// <summary>A saved key is plain text in the setting store, and sqlite makes its files with the umask (rw-r--r--), so the
/// store's folder is 0700 from its first byte: another OS user cannot read the key. Not on Windows (no modes there).</summary>
public class StoreFolderModeProbeTests
{
    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows)]
    public async Task ASettingStoresFolder_HasNoGroupOrOtherBits()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-mode-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning();
        // a store on disk is a build's (a test's is in memory)
        app.Build = new global::app.module.build.@this(app.actor.list.System.Context);
        var context = app.actor.list.User.Context;

        var setting = await context.Setting.Get<global::app.module.llm.setting.@this>();
        setting.Key = "fake-key-modeprobe";
        await (await context.Setting.Save(setting)).IsSuccess();

        // wherever the store landed: every folder holding a sqlite file under the root
        var folders = System.IO.Directory.GetFiles(root, "*.sqlite", System.IO.SearchOption.AllDirectories)
            .Select(f => System.IO.Path.GetDirectoryName(f)!).Distinct().ToList();
        await Assert.That(folders).IsNotEmpty();
        var others = System.IO.UnixFileMode.GroupRead | System.IO.UnixFileMode.GroupWrite | System.IO.UnixFileMode.GroupExecute
                   | System.IO.UnixFileMode.OtherRead | System.IO.UnixFileMode.OtherWrite | System.IO.UnixFileMode.OtherExecute;
        foreach (var folder in folders)
            await Assert.That(System.IO.File.GetUnixFileMode(folder) & others).IsEqualTo((System.IO.UnixFileMode)0);
    }

    // a folder another OS user owns (/tmp is root's) can't take a mode from this one: the mode answers why, never throws
    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows)]
    public async Task AFolderAnotherUserOwns_AnswersItsModeRefusal()
    {
        // as root the mode would hold, and /tmp would be closed to every other user
        if (System.Environment.IsPrivilegedProcess) { Skip.Test("root may set any folder's mode"); return; }
        await using var app = new global::app.@this("/tmp", global::PLang.Tests.Parent.Actor).Testing();
        var system = app.actor.list.System.Context;

        var mode = await system.File("/tmp").Mode((System.IO.UnixFileMode)0b111_000_000, system);

        await mode.IsFailure();
        await Assert.That(mode.Error!.Key).IsEqualTo("IOError");
    }
}
