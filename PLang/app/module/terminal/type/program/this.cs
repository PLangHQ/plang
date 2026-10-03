using System.Diagnostics;
using System.Text;
using app.error;
using Context = app.actor.context.@this;
using Data = app.data.@this;
using FilePath = app.type.item.path.file.@this;
using Sandbox = app.module.terminal.code.Sandbox;
using Text = app.type.item.text.@this;
using Verb = app.type.item.permission.Verb;

namespace app.module.terminal.type.program;

/// <summary>
/// A program a step starts: what it names — the program (on PATH or a path), its arguments, environment, folder, the
/// permissions it is held to, whether it starts clean and what it keeps — found and permitted (<see cref="Prepare"/>),
/// then run to its end (<c>terminal.start</c>, <see cref="Run"/>) or kept running (<c>terminal.open</c>,
/// <see cref="Open"/>). The terminal spawns it.
/// </summary>
public sealed partial class @this(IProgram asked)
{
    /// <summary>The program found: its file.</summary>
    internal FilePath? File { get; private set; }

    /// <summary>How it is started: its file, arguments, folder and environment.</summary>
    internal ProcessStartInfo? Info { get; private set; }

    /// <summary>The hold on it, when the step gives it permissions.</summary>
    internal Sandbox? Sandbox { get; private set; }

    /// <summary>A start trusted by its origin (579): an os goal naming it all.</summary>
    internal bool Trusted { get; private set; }

    /// <summary>It gets a pipe pair beside its standard streams (fd 3 in, fd 4 out).</summary>
    internal bool Pipe { get; private set; }

    /// <summary>The terminal's settings it runs by: the actor's, or the defaults for a start trusted by its origin.</summary>
    internal setting.@this Setting { get; private set; } = new();

    /// <summary>The program found and permitted, with its arguments, environment and folder, held when the step gives
    /// it permissions; or why not (null when ready).</summary>
    internal async Task<Data?> Prepare()
    {
        var context = asked.Context;
        var permission = asked.Permission;
        // what the step wrote, before it is read: a %ref% names permissions even when it holds none
        var named = permission?.Peek() is { } written and not global::app.type.item.@null.@this ? written : null;
        var given = permission == null ? null : await permission.Value();
        // permissions named but not read (one that is no permission, a %ref% holding none) stop the start — a step that
        // gives a program permissions never runs it free; to run free, the step leaves Permission out
        if (permission != null && (!permission.Success || permission.Error != null))
            return permission.Error != null ? context.Error(permission.Error) : permission;
        if (given == null && named != null)
            return context.Error(new ActionError(
                $"The step gives the program permissions, but {named} holds none — leave Permission out to run it free.", "PermissionInvalid", 400));
        if (await Found(held: given != null) is { } failed) return failed;
        // a start trusted by its origin runs by the terminal's defaults, none of the actor's settings (579)
        Setting = Trusted ? new() : context.Setting.Of<setting.@this>();
        if (given == null) return null;
        var (sandbox, refused) = await Sandbox.Of(given.Rows(context), File!, context);
        Sandbox = sandbox;
        return sandbox == null ? refused : null;
    }

    private async Task<Data?> Found(bool held)
    {
        var context = asked.Context;
        var (app, parameter, environment, workingDirectory, clean, keep) =
            (asked.App, asked.Parameter, asked.Environment, asked.WorkingDirectory, asked.Clean, asked.Keep);
        // the step names what it starts itself when the program, its arguments, environment, folder, whether it is clean
        // and what it keeps are all written in it — no variable but the running app's anchors (%!app.AbsolutePath%),
        // nothing its caller handed it: only then may a goal that ships with plang start it unasked (decision 579; any
        // other variable there is the caller's, and is asked as the caller)
        static bool Written(Data? given)
            => given?.Peek() is not { } held || held.Variable.All(global::app.type.item.path.@this.IsAnchor);
        var named = Written(app) && Written(parameter) && Written(environment) && Written(workingDirectory)
            && Written(clean) && Written(keep);
        var cleanStart = clean != null && (await clean.Value())?.Value == true;
        // an option the step doesn't write is read from the actor's settings (terminal.start.setting.<option>, then
        // terminal.setting.<option>): for a start that would be trusted, that is the actor choosing what runs — the
        // folder, the arguments, the environment — so a start is the step's own only when no setting gives it an option
        // (a clean start takes no environment setting at all, so one there changes nothing)
        var asking = context.call.Current?.Action;
        if (named && asking?.Module[asking.Name] is { } element)
            foreach (var option in element.Property)
                if (asking.Property[option.Name] == null
                    && !(cleanStart && string.Equals(option.Name, "Environment", StringComparison.OrdinalIgnoreCase))
                    && (await context.Setting.Get(asking, option.Name.ToLowerInvariant())).IsInitialized)
                {
                    named = false;
                    // said, so an os goal that asks because of a setting isn't a mystery
                    if (context.App.Debug is { } debug)
                        await debug.Write($"terminal: a setting gives {asking.Module}.{asking.Name} its {option.Name}, so the start is the actor's and asked");
                    break;
                }

        // what a clean start keeps of plang's own environment: names, each a plain variable name, never a value
        var kept = keep == null || !await keep.ToBooleanAsync() ? [] : ((await keep.Value())!.Clr<List<object?>>() ?? [])
            .Select(k => k?.ToString() ?? "").ToList();
        if (kept.FirstOrDefault(k => !System.Text.RegularExpressions.Regex.IsMatch(k, "^[A-Za-z_][A-Za-z0-9_]*$")) is { } bad)
            return context.Error(new ActionError($"Keep names variables to keep, by name: '{bad}' is no variable name.", "KeepInvalid", 400));

        var name = (await app.Value())!.Clr<string>()!;
        var program = FilePath.Program(name, context);
        if (program == null)
            return context.Error(new ActionError($"Program not found: {name}. Not a path, and not on PATH.", "ProgramNotFound", 404));
        var allowed = await program.Authorize(Verb.execute, context, named);
        if (allowed.Exits || !allowed.Success) return allowed;

        var folder = workingDirectory == null ? null : await workingDirectory.Value();
        if (folder != null)
        {
            var readable = await folder.Authorize(Verb.read, context);
            if (readable.Exits || !readable.Success) return readable;
        }

        var info = new ProcessStartInfo(program.Absolute) { WorkingDirectory = folder?.Absolute ?? context.App.AbsolutePath };
        var parameters = parameter == null || !await parameter.ToBooleanAsync() ? null : (await parameter.Value())!.Clr<List<object?>>();
        foreach (var p in parameters ?? []) info.ArgumentList.Add(p?.ToString() ?? "");

        // a program held to permissions, or started clean, starts with none of plang's own environment (its keys among
        // it): only the sandbox's base, what it keeps of plang's by name, and what the step gives it
        if (held || cleanStart)
        {
            var own = new Dictionary<string, string?>(info.Environment);
            info.Environment.Clear();
            foreach (var (key, value) in Sandbox.Environment) info.Environment[key] = value;
            foreach (var variable in kept)
                if (own.TryGetValue(variable, out var value) && value != null) info.Environment[variable] = value;
                else if (context.App.Debug is { } debug) await debug.Write($"{program.Name}: plang has no {variable} to keep");
        }
        // a start trusted by its origin (an os goal naming it all) takes nothing of the actor's settings that changes what
        // runs: the settings are the actor's, a user program sets them, and an LD_PRELOAD there would run the user's code
        // inside what started unasked. Its environment is plang's own and what the step names, nothing else. A clean
        // start takes no environment setting either.
        var trusted = named && global::app.type.item.path.@this.AskedByOs(context);
        var env = trusted || cleanStart ? new() : context.Setting.Of<setting.@this>().Environment.Clr<Dictionary<string, object?>>() ?? new();
        // a step that writes no Environment reads the setting's through its property — for a trusted or clean start that
        // is the same side door: it takes only what the step itself wrote
        var written = asking?.Property["Environment"] != null || environment?.Peek() is { IsNull: false } && asking == null;
        if (environment != null && (written || !(trusted || cleanStart)) && await environment.ToBooleanAsync())
            foreach (var (key, value) in (await environment.Value())!.Clr<Dictionary<string, object?>>() ?? new())
                env[key] = value;
        foreach (var (key, value) in env) info.Environment[key] = value?.ToString();
        (File, Info, Trusted) = (program, info, trusted);
        return null;
    }

    // stdin is always the program's own: written by plang, never inherited — inherited, it would read plang's console,
    // taking keystrokes meant for plang's own prompts.
    private void Redirect()
    {
        var encoding = Encoding.GetEncoding((string)Setting.Encoding.Clr<string>()!);
        var info = Info!;
        info.UseShellExecute = false;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.StandardOutputEncoding = encoding;
        info.StandardErrorEncoding = encoding;
        info.StandardInputEncoding = new UTF8Encoding(false);
    }

    /// <summary>It is a plang told to speak plang's own format (<c>--app.type.format=…</c>, a name of that format).</summary>
    internal bool SpeaksPlang(Context context)
    {
        const string flag = "--app.type.format=";
        var named = Info!.ArgumentList.FirstOrDefault(a => a.StartsWith(flag, StringComparison.OrdinalIgnoreCase))?[flag.Length..];
        return named != null && ReferenceEquals(context.App.type.Named(named), process.@this.PlangFormat(context));
    }

    public override string ToString() => File?.Absolute ?? asked.App.Peek()?.ToString() ?? "a program";
}
