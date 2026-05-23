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

		public async Task<app.data.@this> Run(string[] args, CancellationToken cancellationToken = default)
		{
			var (engine, configError) = Configure(args);
			if (configError != null) return configError;
			return await engine!.Start();
		}

		/// <summary>
		/// Parses argv and prepares an App engine for execution: wires CLI parameters
		/// to user variables, applies --test / --debug / --build / --app config, and
		/// sets the goalFile variable on System.Context that Start() reads.
		/// Returns (engine, null) on success, (null, errorData) if --test= config is invalid.
		/// Separated from Run() so tests can observe configuration without executing Start().
		/// </summary>
		internal (app.@this? Engine, app.data.@this? Error) Configure(string[] args)
		{
			// Normalize: "build" or "--builder" both become the --builder flag.
			// Legacy `plang build` form preserved as ergonomics; --builder is canonical.
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--builder", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new app.@this(startupDirectory);
			engine.OsDirectory = engine.OsAbsolutePath;

			var userVars = engine.User.Context.Variables;

			// Route CLI parameters to user Variables
			foreach (var param in parameters)
			{
				if (param.Key.StartsWith("!")) continue; // app config, not variables
				userVars.Set(param.Key, param.Value);
			}

			// Debug mode
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
				engine.Debug.Apply(debugValue);

			// Tester mode (--tester or legacy --test)
			if ((parameters.TryGetValue("!tester", out var testValue) && testValue is not false) ||
			    (parameters.TryGetValue("!test", out testValue) && testValue is not false))
			{
				engine.Tester.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", startupDirectory);

				if (testValue is IDictionary<string, object?> testDict)
				{
					var applyResult = engine.Tester.Apply(testDict);
					if (!applyResult.Success) return (null, applyResult);
				}
			}

			// App settings (--app={"create":true})
			if (parameters.TryGetValue("!app", out var appValue) && appValue is IDictionary<string, object?> appDict)
				global::app.types.@this.Populate(engine, appDict, engine.User.Context);

			// Builder mode (--builder or legacy --build). Either flag may be a bare
			// `true` (e.g. `plang build` normalizes the subcommand to `--builder`) or
			// carry a JSON config dict (`--build={"files":[...]}`). Both keys must be
			// read into separate variables — folding them into one `||` with a shared
			// `out` variable lets the short-circuit drop whichever key carries the dict.
			parameters.TryGetValue("!builder", out var builderValue);
			parameters.TryGetValue("!build", out var buildValue);
			if (builderValue is not (null or false) || buildValue is not (null or false))
			{
				engine.Builder.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", startupDirectory);

				// Whichever flag carried the JSON object holds the build config.
				var buildDict = builderValue as IDictionary<string, object?>
				             ?? buildValue as IDictionary<string, object?>;
				if (buildDict != null)
					global::app.types.@this.Populate(engine.Builder, buildDict, engine.User.Context);

				// Sync cache flag to %!build.cache% for Build.goal
				userVars.Set("!build.cache", engine.Builder.Cache);
			}

			// Set the goal file on system context — Start() reads it
			// Tester mode routes to system test runner instead of Start.goal
			if (engine.Tester.IsEnabled && goalFile == "Start.goal")
			{
				engine.System.Context.Variables.Set("goalFile", "/system/.build/test.pr");
				return (engine, null);
			}

			var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
			if (!prPath.StartsWith(".build"))
				prPath = ".build/" + prPath;
			engine.System.Context.Variables.Set("goalFile", "/" + prPath.ToLowerInvariant());

			return (engine, null);
		}
	}
}
