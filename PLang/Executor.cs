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
			var (app, configError) = await Configure(args);
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
		internal async Task<(global::app.@this? Engine, global::app.data.@this? Error)> Configure(string[] args)
		{
			// The `plang build` subcommand is an ergonomic alias for the canonical `--build`
			// flag. (The `--builder` flag spelling is gone — `--build` is the one form.)
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--build", .. args[1..]];

			var (positional, parameters) = CommandLineParser.Parse(args);

			// The CLI is the one interactive terminal owner: opt out of the
			// ctor's non-interactive auto-wire and bind real stdin explicitly so
			// `output.ask` prompts read the user's keystrokes. Ad-hoc/test apps
			// keep the EOF-sink input from auto-wire.
			var app = new global::app.@this(startupDirectory, autoWireConsoleChannels: false);
			global::app.@this.WireDefaultConsoleChannels(app.actor.list.System);
			global::app.@this.WireDefaultConsoleChannels(app.actor.list.User);

			// A flag is a setting's path: --channel.setting.format=plang is this run's value for
			// %!app.channel.setting.format%, on the system actor (the user falls back to it); the mode words alone
			// (--build, --test, --debug) switch their mode on. A flag no setting takes is the run's answer.
			// each mode word's settings class
			var modes = new Dictionary<string, System.Type>(StringComparer.OrdinalIgnoreCase)
			{
				["!debug"] = typeof(global::app.module.debug.setting.@this),
				["!test"] = typeof(global::app.test.setting.@this),
				["!build"] = typeof(global::app.module.build.setting.@this),
			};
			// the flags that are settings' paths, and the setting classes they will write — known before anything is written
			var flags = new List<(string Address, object? Value)>();
			var settings = new HashSet<System.Type>();
			foreach (var (key, value) in parameters.Where(p => p.Key.StartsWith('!')).ToList())
			{
				if (modes.TryGetValue(key, out var mode))
				{
					if (value is IDictionary<string, object?>)
						return (null, app.actor.list.System.Context.Error(new global::app.error.@this(
							$"'--{key[1..]}' takes no settings: write them at its setting's path, --{app.actor.list.System.Setting.Address(mode)}={{…}} or --{app.actor.list.System.Setting.Address(mode, "<option>")}=value.",
							"UnknownSetting", 400)));
					continue;
				}
				// the setting the flag names, the option under it, and the value it takes — a typo is refused before
				// anything is written
				var context = app.actor.list.System.Context;
				var setting = app.actor.list.System.Setting.Find(key[1..]);
				if (!setting.Success) return (null, setting);
				var values = (await setting.Value())!.Apply(key[1..], value, context);
				if (!values.Success) return (null, values);
				settings.Add((await setting.Value())!.GetType());
				foreach (var entry in (await values.Value())!.Entries(context)) flags.Add((entry.Name, entry));
			}

			// A mode is on when its word is given (and not false), or when the run writes its setting.
			bool On(string word) => parameters.TryGetValue(word, out var given) && given is not false
				|| settings.Contains(modes[word]);

			// The modes' defaults, under the command line's values: a debug run shows each step's time, so this run's
			// call stacks time their frames (--call.setting.timing=false still says not).
			if (On("!debug"))
				app.actor.list.System.Setting.Set(app.actor.list.System.Setting.Address(typeof(global::app.call.setting.@this), "timing"), true);

			// The command line's values, once: the last word.
			foreach (var (address, value) in flags)
				app.actor.list.System.Setting.Set(address, value);

			var userVars = app.actor.list.User.Context.Variable;

			// Route CLI parameters to user Variables
			foreach (var param in parameters)
			{
				if (param.Key.StartsWith("!")) continue; // app config, not variables
				await userVars.Set(param.Key, param.Value);
			}

			// Debug mode — born reading its setting; then activated (watchers, LLM hooks, grep regex, event bindings).
			if (On("!debug"))
			{
				app.Debug = new Debug(app.actor.list.System.Context);
				if (await app.Debug.Activate() is { } refused) return (null, app.actor.list.System.Context.Error(refused));
			}

			// Test mode: its setting names the actor the run's session opens on. What it tests is the positional (a folder
			// or a .test.goal), unless a path= is given; neither, the whole folder it starts in.
			if (On("!test"))
			{
				// a folder or test named on the command line is the starting folder's, resolved once here
				if (!parameters.ContainsKey("path"))
					await userVars.Set("path", positional == null ? startupDirectory
						: global::app.type.item.path.@this.Resolve("/" + positional.TrimStart('/', '\\'), app.actor.list.User.Context));
				await app.test.list.Open();
			}

			// Build mode (`plang build` normalizes the subcommand to `--build`).
			if (On("!build"))
			{
				app.Build = new global::app.module.build.@this(app.actor.list.System.Context);
				if (!parameters.ContainsKey("path"))
					await userVars.Set("path", startupDirectory);

				// A build that skips the cache flows DOWN to llm.query as llm's cache setting, so llm.query reads its
				// own `action.Cache` (which resolves %!llm.query.setting.cache% → %!llm.setting.cache% →
				// [Default]) instead of sniffing the build. The skip reaches every llm.query without threading.
				// The run's value is in memory, so the sync Configure sets it at once.
				var cache = (await app.actor.list.System.Context.Setting.Get<global::app.module.build.setting.@this>()).Cache;
				if (cache.Value == global::app.module.cache.type.cache.skip)
					app.actor.list.System.Setting.Set(app.actor.list.System.Setting.Address(typeof(global::app.module.llm.setting.@this), "cache"), cache);
			}

			// Set the goal file on system context — Start() reads it. Test mode runs the system test runner on what it
			// tests.
			if (app.Mode.Value == global::app.Mode.Test)
			{
				await app.actor.list.System.Context.Variable.Set("goalFile", "/system/test.goal");
				return (app, null);
			}

			// the .goal to run (Start when none is named); the goal finds its own .pr
			var goalFile = positional ?? "Start";
			var source = goalFile.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? goalFile : goalFile + ".goal";
			await app.actor.list.System.Context.Variable.Set("goalFile", "/" + source.TrimStart('/', '\\'));

			return (app, null);
		}
	}
}
