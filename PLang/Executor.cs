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

			// The CLI is the one interactive terminal owner: opt out of the
			// ctor's non-interactive auto-wire and bind real stdin explicitly so
			// `output.ask` prompts read the user's keystrokes. Ad-hoc/test apps
			// keep the EOF-sink input from auto-wire.
			var app = new global::app.@this(startupDirectory, autoWireConsoleChannels: false);
			global::app.@this.WireDefaultConsoleChannels(app.actor.list.System);
			global::app.@this.WireDefaultConsoleChannels(app.actor.list.User);

			// a line it can't read is the run's answer, the argument named
			global::app.start.@this start;
			try { start = global::app.start.@this.Parse(args); }
			catch (global::app.error.AppException ex) { return (null, app.actor.list.System.Context.Error(ex.Error)); }

			// A flag is a setting's path: --channel.setting.format=plang is this run's value for
			// %!app.channel.setting.format%, on the system actor (the user falls back to it); the mode words alone
			// (--build, --test, --debug) switch their mode on. A flag no setting takes is the run's answer.
			var store = app.actor.list.System.Setting;
			// each mode word: its settings class, and the form that says what a value given to the word meant
			var modes = new Dictionary<string, (System.Type Setting, Func<object?, string> Meant)>(StringComparer.OrdinalIgnoreCase)
			{
				["debug"] = (typeof(global::app.module.debug.setting.@this),
					value => $"To debug one goal, --{store.Address(typeof(global::app.module.debug.setting.@this), "goal")}={value}."),
				["test"] = (typeof(global::app.test.setting.@this),
					value => $"To test {value}, --test path={value} (or --test {value})."),
				["build"] = (typeof(global::app.module.build.setting.@this),
					_ => $"Its settings are --{store.Address(typeof(global::app.module.build.setting.@this), "<option>")}=value."),
			};
			// the mode words the line gives — the Executor's own, never a setting's path; a mode word takes no settings of
			// its own: they are written at its setting's path
			var words = start.property.Items().Where(property => property.IsFlag && modes.ContainsKey(property.Name)).ToList();
			foreach (var word in words)
			{
				if (word.Value is IDictionary<string, object?>)
					return (null, app.actor.list.System.Context.Error(new global::app.error.@this(
						$"'--{word.Name}' takes no settings: write them at its setting's path, --{store.Address(modes[word.Name].Setting)}={{…}} or --{store.Address(modes[word.Name].Setting, "<option>")}=value.",
						"UnknownSetting", 400)));
				// a mode word is on or off; any other value it would drop — the line says what it meant another way
				if (word.Value is not bool)
					return (null, app.actor.list.System.Context.Error(new global::app.error.@this(
						$"'--{word.Name}={word.Value}' gives --{word.Name} a value it takes none of: it is on (--{word.Name}) or off (--{word.Name}=false). "
						+ modes[word.Name].Meant(word.Value),
						"ArgumentRefused", 400)));
			}
			// the flags that are settings' paths (the mode words left out), each checked by its setting, then all
			// written — the command line's values, the last word; a typo is the run's answer, and nothing is written
			var set = await start.Set(store, app.actor.list.System.Context, modes.Keys);
			if (!set.Success) return (null, set);
			var values = (await set.Value())!;

			// A mode is on when its word is given (and not false), or when the run writes its setting.
			bool On(string mode) => words.Any(word => string.Equals(word.Name, mode, StringComparison.OrdinalIgnoreCase) && word.Value is not false)
				|| values.KeyNames.Any(address => store.Of(address) == modes[mode].Setting);
			// a mode turned off while the line writes its setting says two things: the run's answer, naming both
			foreach (var word in words.Where(word => word.Value is false))
				if (values.KeyNames.FirstOrDefault(address => store.Of(address) == modes[word.Name].Setting) is { } address)
					return (null, app.actor.list.System.Context.Error(new global::app.error.@this(
						$"'--{word.Name}=false' turns {word.Name} off, and '--{address}' turns it on: write one of them.",
						"ArgumentRefused", 400)));

			// The modes' defaults, under the command line's values: a debug run shows each step's time, so this run's
			// call stacks time their frames, unless the line set timing itself (--call.setting.timing=false still says not).
			var timing = store.Address(typeof(global::app.call.setting.@this), "timing");
			if (On("debug") && !values.Has(timing))
				store.Set(timing, true);

			var userVars = app.actor.list.User.Context.Variable;

			// The line's values are the user's variables; its flags are the app's settings, not variables
			foreach (var property in start.property.Items().Where(property => !property.IsFlag))
				await userVars.Set(property.Name, property.Value);

			// Debug mode — born reading its setting; then activated (watchers, LLM hooks, grep regex, event bindings).
			if (On("debug"))
			{
				app.Debug = new Debug(app.actor.list.System.Context);
				if (await app.Debug.Activate() is { } refused) return (null, app.actor.list.System.Context.Error(refused));
			}

			// Test mode: its setting names the actor the run's session opens on. What it tests is the positional (a folder
			// or a .test.goal), unless a path= is given; neither, the whole folder it starts in.
			if (On("test"))
			{
				// a folder or test named on the command line is the starting folder's, resolved once here
				// none named, the root the app landed on — the build and the test runner compare paths with it
				if (!start.property.Has("path"))
					await userVars.Set("path", start.Goal == null ? app.filesystem.root
						: global::app.type.item.path.@this.Resolve("/" + start.Goal.TrimStart('/', '\\'), app.actor.list.User.Context));
				await app.test.list.Open();
			}

			// Build mode (`plang build` normalizes the subcommand to `--build`).
			if (On("build"))
			{
				app.Build = new global::app.module.build.@this(app.actor.list.System.Context);
				if (!start.property.Has("path"))
					await userVars.Set("path", app.filesystem.root);

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
			var goalFile = start.Goal ?? "Start";
			var source = goalFile.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? goalFile : goalFile + ".goal";
			await app.actor.list.System.Context.Variable.Set("goalFile", "/" + source.TrimStart('/', '\\'));

			return (app, null);
		}
	}
}
