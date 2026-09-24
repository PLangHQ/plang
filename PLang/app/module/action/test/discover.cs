using System.Reflection;
using app.Attributes;
using app.error;
using app.test;
using app.Utils;
using app.variable;
using Goal = app.goal.@this;
using FilePath = app.type.item.path.file.@this;

namespace app.module.action.test;

/// <summary>
/// Walks a directory tree for *.test.goal files (via <c>rootPath.List</c>, which
/// routes through <see cref="app.type.item.path.file.@this.AuthGate"/>), loads each
/// file's .pr through path verbs and checks freshness against the current .goal
/// text (SHA-256 of Name + concat(Step.Text)). A fresh goal becomes its test, as the
/// run takes it (<see cref="app.test.list.@this.Create"/>). Returns a list&lt;test&gt;
/// that test.run consumes.
///
/// <para>The pre-AuthGate scan that this handler used to do —
/// <c>StartsWith(rootPrefix)</c> hand-rolled containment + raw
/// <c>System.IO.Directory.EnumerateFiles</c> — is gone. AuthGate is the only
/// gate, and an out-of-root <c>--test</c> path now surfaces as a permission
/// prompt or denial instead of a silent empty list.</para>
/// </summary>
[Action("discover")]
public partial class discover : IContext
{
    /// <summary>Directory to walk. AuthGate(Read) enforces in-root vs prompt-or-deny.</summary>
    [Default(".")]
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }

    /// <summary>Filename pattern. Default matches PLang test convention.</summary>
    [Default("*.test.goal")]
    public partial data.@this<global::app.type.item.text.@this> Pattern { get; init; }

    /// <summary>Walk subdirectories. Default true.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> Recursive { get; init; }

    public async Task<data.@this<global::app.type.item.list.@this<global::app.test.@this>>> Run()
    {
        var empty = data.@this<global::app.type.item.list.@this<global::app.test.@this>>.Ok(new global::app.type.item.list.@this<global::app.test.@this>());

        var root = await Path.Value();
        if (root == null) return empty;

        // List routes through AuthGate(Read). Out-of-root: prompt or denial.
        var listed = await root.List((await Pattern.Value())!.Clr<string>()!, (await Recursive.Value())!.Value, Context);
        if (!listed.Success) return Context.Error<global::app.type.item.list.@this<global::app.test.@this>>(listed.Error!);
        if (await listed.Value() == null) return empty;

        var files = new List<data.@this>();
        var list = await listed.Value();
        foreach (var row in list!.Items(Context))
        {
            // .test.goal files only resolve under the file scheme; foreign schemes
            // skip silently. The List call already returned filesystem paths.
            if (await row.Value<global::app.type.item.path.@this>() is not FilePath fileMatch) continue;
            files.Add(new data.@this("", await DiscoverOne(fileMatch), context: Context));
        }
        return Context.Ok<global::app.type.item.list.@this<global::app.test.@this>>(
            new global::app.type.item.list.@this<global::app.test.@this>(files));
    }

    /// <summary>Discovers metadata for a single .test.goal file (FilePath form).</summary>
    private async Task<global::app.test.@this> DiscoverOne(FilePath goalFile)
    {
        // Read the .goal source first — even when the .pr is missing or
        // corrupt, the source goal is enough to identify the file.
        // ReadText returns a typed Goal when the file MIME is application/
        // plang-goal, or a string fallback that we explicitly parse.
        var goalRead = await goalFile.ReadText(Context);
        if (!goalRead.Success)
        {
            // Build a minimal goal from just the file's path so Test.Goal
            // is never null. Status=Stale with the read error as reason.
            return new global::app.test.@this()
            {
                Goal = new Goal { Path = goalFile },
                Status = global::app.test.Status.Stale,
                StatusReason = "goal read error: " + (goalRead.Error?.Message ?? "")
            };
        }
        // Born-typed: text content rides as the text wrapper; its string form
        // is ToString (a Goal already matched the first arm).
        var sourceGoal = (await goalRead.Value()) as Goal
            ?? Goal.Parse((await goalRead.Value())?.ToString() ?? "", goalFile, Context)
            ?? new Goal { Path = goalFile };

        // PrPath is derived on the goal from its Path. The corresponding
        // build artefact may or may not exist.
        var prFile = sourceGoal.PrPath as FilePath;

        if (prFile == null)
        {
            return new global::app.test.@this()
            {
                Goal = sourceGoal,
                Status = global::app.test.Status.Stale,
                StatusReason = "no PrPath derivable from goal source"
            };
        }

        var prExists = await prFile.ExistsAsync(Context);
        if (!prExists.Success || (await prExists.Value())?.Value != true)
        {
            return new global::app.test.@this()
            {
                Goal = sourceGoal,
                Status = global::app.test.Status.Stale,
                StatusReason = "no .pr"
            };
        }

        // Read the .pr through the gated verb. MIME maps .pr → Goal via
        // ReadText's TryConvert branch.
        var prRead = await prFile.ReadText(Context);
        if (!prRead.Success)
        {
            return new global::app.test.@this()
            {
                Goal = sourceGoal,
                Status = global::app.test.Status.Stale,
                StatusReason = prRead.Error?.Message ?? "pr corrupt"
            };
        }
        // A .pr the reader refuses (an old format names itself: PrFormatOutdated) is a test that
        // could not load — Stale with the reader's reason, never an aborted discovery.
        Goal? prGoal;
        try { prGoal = (await prRead.Value()) as Goal; }
        catch (global::app.error.AppException refused)
        {
            return new global::app.test.@this()
            {
                Goal = sourceGoal,
                Status = global::app.test.Status.Stale,
                StatusReason = refused.Message
            };
        }
        if (prGoal == null)
        {
            return new global::app.test.@this()
            {
                Goal = sourceGoal,
                Status = global::app.test.Status.Stale,
                StatusReason = prRead.Error?.Message ?? "pr corrupt"
            };
        }

        if (!string.Equals(sourceGoal.Hash, prGoal.Hash, StringComparison.OrdinalIgnoreCase))
        {
            return new global::app.test.@this()
            {
                Goal = sourceGoal,
                Status = global::app.test.Status.Stale,
                StatusReason = "rebuild needed"
            };
        }

        // The fresh goal becomes its test, as this run takes it.
        return await Context.App.Test!.Create(prGoal, Context);
    }

}
