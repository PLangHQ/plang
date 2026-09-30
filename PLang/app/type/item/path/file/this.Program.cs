using app.Utils;

namespace app.type.item.path.file;

public sealed partial class @this
{
    /// <summary>
    /// The program a terminal starts, found the way a shell finds it: a name with a folder in it
    /// resolves as a plang path; a bare name (<c>wsl.exe</c>, <c>git</c>) is looked up on the OS
    /// <c>PATH</c>, trying each <c>PATHEXT</c> extension on Windows when the name has none.
    /// Null when nothing is found.
    ///
    /// Finding is not permission: the lookup only tests which files exist on <c>PATH</c>. Starting the
    /// program must still pass <see cref="global::app.type.item.path.@this.Authorize"/> with
    /// <c>Verb.Execute</c>, which asks the actor for a program outside the app root.
    /// </summary>
    public static @this? Program(string name, actor.context.@this context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(context);

        if (name.Contains('/') || name.Contains('\\'))
        {
            var given = Resolve(name, context);
            return System.IO.File.Exists(given.Absolute) ? given : null;
        }

        var extensions = ProgramExtensions(name);
        var folders = (System.Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(PathHelper.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var folder in folders)
            foreach (var extension in extensions)
            {
                var candidate = PathHelper.Combine(folder, name + extension);
                if (System.IO.File.Exists(candidate)) return new @this(Canonicalize(candidate), context);
            }
        return null;
    }

    // Windows runs "wsl" as "wsl.exe": try the name as given, then each PATHEXT extension.
    // Other systems run the name as given.
    private static string[] ProgramExtensions(string name)
    {
        if (!OperatingSystem.IsWindows() || PathHelper.GetExtension(name).Length > 0) return [""];
        var pathExt = System.Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        return [.. pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
