using app.Utils;
using System.Reflection;

namespace PLang
{
	public class Executor
	{
		private readonly string startupDirectory;

		public Executor(string startupDirectory)
		{
			this.startupDirectory = startupDirectory;
		}

		public async Task<global::app.data.@this> Start(string[] args, CancellationToken cancellationToken = default)
		{
			var (app, configError) = Configure(args);
			if (configError != null) return configError;
			// the run's cancellation cancels its actors: their tasks end Cancelled, as `cancel %task%` ends one
			using var cancelled = cancellationToken.Register(() => { foreach (var actor in app!.actor.list.Items()) actor.Cancel(); });
			return await app!.Start();
		}

		/// <summary>
		/// Parses argv and prepares an App app for execution: wires CLI parameters
		/// to user variables, applies --test / --debug / --build / --app config, and
		/// sets the goalFile variable on System.Context that Start() reads.
		/// Returns (app, null) on success, (null, errorData) if --test= config is invalid.
		/// Separated from Start() so tests can observe configuration without starting the app.
		/// </summary>
		internal (global::app.@this? Engine, global::app.data.@this? Error) Configure(string[] args)
		{
			// The `plang build` subcommand is an ergonomic alias for the canonical `--build`
			// flag. (The `--builder` flag spelling is gone — `--build` is the one form.)
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--build", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			// The CLI is the one interactive terminal owner: opt out of the
			// ctor's non-interactive auto-wire and bind real stdin explicitly so
			// `output.ask` prompts read the user's keystrokes. Ad-hoc/test apps
			// keep the EOF-sink input from auto-wire.
			var app = new global::app.@this(startupDirectory, autoWireConsoleChannels: false);
			global::app.@this.WireDefaultConsoleChannels(app.actor.list.System);
			global::app.@this.WireDefaultConsoleChannels(app.actor.list.User);

			var userVars = app.actor.list.User.Context.Variable;

			// Route CLI parameters to user Variables
			foreach (var param in parameters)
			{
				if (param.Key.StartsWith("!")) continue; // app config, not variables
				userVars.Set(param.Key, param.Value);
			}

			// Each flag's dict is this run's values for its owner's setting class, on the system actor (the
			// user falls back to it): --debug → %!debug%, --test → %!app.test.setting%, --app →
			// %!app.setting%, --callstack → %!app.call.setting% (both actors' call stacks read it),
			// --build → %!build.setting%. A key that isn't one of the class's options is refused.
			global::app.data.@this? Flag<TSetting>(string name) where TSetting : global::app.type.item.setting.@this, new()
			{
				if (!parameters.TryGetValue(name, out var value) || value is not IDictionary<string, object?> dict) return null;
				var set = app.actor.list.System.Setting.Set(new TSetting().Path, dict);
				return set.Success ? null : set;
			}

			// Debug mode — born under --debug (presence = enabled), reading its setting; then activated
			// (watchers, LLM hooks, grep regex, event bindings).
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
			{
				if (Flag<global::app.module.debug.setting.@this>("!debug") is { } debugError) return (null, debugError);
				app.Debug = new Debug(app.actor.list.System.Context);
				if (app.Debug.Activate() is { } refused) return (null, app.actor.list.System.Context.Error(refused));
			}

			// Test mode (--test is canonical; --tester is gone). The setting first: it names the actor the
			// run's session opens on.
			if (parameters.TryGetValue("!test", out var testValue) && testValue is not false)
			{
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", startupDirectory);
				if (Flag<global::app.test.setting.@this>("!test") is { } testError) return (null, testError);
				app.test.list.Open();
			}

			if (Flag<global::app.setting.@this>("!app") is { } appError) return (null, appError);

			// A debug run shows each step's time: this run's call stacks time their frames. An explicit
			// --callstack={"timing":false} below still has the last word.
			if (app.Debug != null)
				app.actor.list.System.Setting.Set(new global::app.call.setting.@this().Path, new Dictionary<string, object?> { ["timing"] = true });

			// Each actor owns its own call tree; both read the one setting (the user's falls back to the
			// system's). (Service actors are spawned later — carrying the flag to them is a separate concern.)
			if (Flag<global::app.call.setting.@this>("!callstack") is { } callstackError) return (null, callstackError);

			// Build mode (--build is canonical; --builder is gone). The flag may be a bare
			// `true` (`plang build` normalizes the subcommand to `--build`) or carry a JSON
			// config dict (`--build={"files":[...]}`).
			parameters.TryGetValue("!build", out var buildValue);
			if (buildValue is not (null or false))
			{
				app.Build = new global::app.module.build.@this(app.actor.list.System.Context);
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", startupDirectory);
				if (Flag<global::app.module.build.setting.@this>("!build") is { } buildError) return (null, buildError);

				// A build that skips the cache flows DOWN to llm.query as llm's cache setting, so llm.query reads its
				// own `action.Cache` (which resolves %!llm.query.setting.cache% → %!llm.setting.cache% →
				// [Default]) instead of sniffing the build. The skip reaches every llm.query without threading.
				// The run's value is in memory, so the sync Configure sets it at once.
				var cache = app.actor.list.System.Context.Setting.Of<global::app.module.build.setting.@this>().Cache;
				if (cache.Value == global::app.module.cache.type.cache.skip)
					app.actor.list.System.Setting.Set(new global::app.module.llm.setting.@this().Path + ".cache", app.actor.list.System.Context.Ok(cache))
						.GetAwaiter().GetResult();
			}

			// Set the goal file on system context — Start() reads it
			// Tester mode routes to system test runner instead of Start.goal
			if (app.Mode.Value == global::app.Mode.Test && goalFile == "Start.goal")
			{
				app.actor.list.System.Context.Variable.Set("goalFile", "/system/test.goal");
				return (app, null);
			}

			// the .goal to run; the goal finds its own .pr
			var source = goalFile.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? goalFile : goalFile + ".goal";
			app.actor.list.System.Context.Variable.Set("goalFile", "/" + source.TrimStart('/', '\\'));

			return (app, null);
		}
	}
}
