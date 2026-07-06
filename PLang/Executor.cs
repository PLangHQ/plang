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

		public async Task<global::app.data.@this> Run(string[] args, CancellationToken cancellationToken = default)
		{
			var (app, configError) = Configure(args);
			if (configError != null) return configError;
			return await app!.Start();
		}

		/// <summary>
		/// Parses argv and prepares an App app for execution: wires CLI parameters
		/// to user variables, applies --test / --debug / --build / --app config, and
		/// sets the goalFile variable on System.Context that Start() reads.
		/// Returns (app, null) on success, (null, errorData) if --test= config is invalid.
		/// Separated from Run() so tests can observe configuration without executing Start().
		/// </summary>
		internal (global::app.@this? Engine, global::app.data.@this? Error) Configure(string[] args)
		{
			// Normalize: "build" or "--builder" both become the --builder flag.
			// Legacy `plang build` form preserved as ergonomics; --builder is canonical.
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--builder", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			// The CLI is the one interactive terminal owner: opt out of the
			// ctor's non-interactive auto-wire and bind real stdin explicitly so
			// `output.ask` prompts read the user's keystrokes. Ad-hoc/test apps
			// keep the EOF-sink input from auto-wire.
			var app = new global::app.@this(startupDirectory, autoWireConsoleChannels: false);
			global::app.@this.WireDefaultConsoleChannels(app.System);
			global::app.@this.WireDefaultConsoleChannels(app.User);
			app.OsDirectory = app.OsAbsolutePath;

			var userVars = app.User.Context.Variable;

			// Route CLI parameters to user Variables
			foreach (var param in parameters)
			{
				if (param.Key.StartsWith("!")) continue; // app config, not variables
				userVars.Set(param.Key, param.Value);
			}

			// Debug mode — born under --debug (presence = enabled). Config (scalars) flows
			// through the setting walk like every other flag; then the Debug is activated
			// (watchers, LLM hooks, grep regex, event bindings).
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
			{
				app.Debug = new Debugging(app.System.Context);
				if (debugValue is IDictionary<string, object?> debugDict)
				{
					var debugResult = app.Setting.Set(app.Debug, debugDict);
					if (!debugResult.Success) return (null, debugResult);
				}
				app.Debug.Activate();
			}

			// Tester mode (--tester or legacy --test)
			if ((parameters.TryGetValue("!tester", out var testValue) && testValue is not false) ||
			    (parameters.TryGetValue("!test", out testValue) && testValue is not false))
			{
				app.Test = new global::app.test.list.@this(app.System.Context);
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", startupDirectory);

				if (testValue is IDictionary<string, object?> testDict)
				{
					var applyResult = app.Setting.Set(app.Test, testDict);
					if (!applyResult.Success) return (null, applyResult);
				}
			}

			// App settings (--app={"create":true}) — the convert-walk (public-setter gate + per-leaf
			// TryConvert), not the lift-then-lower catalog.Populate.
			if (parameters.TryGetValue("!app", out var appValue) && appValue is IDictionary<string, object?> appDict)
			{
				var appResult = app.Setting.Set(app, appDict);
				if (!appResult.Success) return (null, appResult);
			}

			// Callstack knobs (--callstack={"timing":true}) — each actor owns its own call
			// tree, so the run-wide flag applies to both startup actors (System bootstrap +
			// User code). Walk is the same as --build/--app. (Service actors are spawned
			// later — carrying the flag to them is a separate concern, TODO.)
			if (parameters.TryGetValue("!callstack", out var callstackValue) && callstackValue is IDictionary<string, object?> callstackDict)
			{
				var systemResult = app.Setting.Set(app.System.CallStack, callstackDict);
				if (!systemResult.Success) return (null, systemResult);
				var userResult = app.Setting.Set(app.User.CallStack, callstackDict);
				if (!userResult.Success) return (null, userResult);
			}

			// Builder mode (--builder or legacy --build). Either flag may be a bare
			// `true` (e.g. `plang build` normalizes the subcommand to `--builder`) or
			// carry a JSON config dict (`--build={"files":[...]}`). Both keys must be
			// read into separate variables — folding them into one `||` with a shared
			// `out` variable lets the short-circuit drop whichever key carries the dict.
			parameters.TryGetValue("!builder", out var builderValue);
			parameters.TryGetValue("!build", out var buildValue);
			if (builderValue is not (null or false) || buildValue is not (null or false))
			{
				app.Build = new global::app.module.builder.@this(app.System.Context);
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", startupDirectory);

				// Whichever flag carried the JSON object holds the build config.
				var buildDict = builderValue as IDictionary<string, object?>
				             ?? buildValue as IDictionary<string, object?>;
				if (buildDict != null)
				{
					var buildResult = app.Setting.Set(app.Build, buildDict);
					if (!buildResult.Success) return (null, buildResult);
				}

				// Sync cache flag to %!build.cache% for Build.goal
				userVars.Set("!build.cache", app.Build.Cache);
			}

			// Set the goal file on system context — Start() reads it
			// Tester mode routes to system test runner instead of Start.goal
			if (app.Test != null && goalFile == "Start.goal")
			{
				app.System.Context.Variable.Set("goalFile", "/system/.build/test.pr");
				return (app, null);
			}

			var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
			if (!prPath.StartsWith(".build"))
				prPath = ".build/" + prPath;
			app.System.Context.Variable.Set("goalFile", "/" + prPath.ToLowerInvariant());

			return (app, null);
		}
	}
}
