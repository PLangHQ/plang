using LightInject;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Utils;
using System.Reflection;

namespace PLang
{
	public class Executor
	{
		private readonly IPLangFileSystem fileSystem;

		public Executor(IServiceContainer container)
		{
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
		}

		public enum ExecuteType
		{
			Runtime = 0,
			Builder = 1
		}

		public async Task<(object? Variables, IError? Error)> Execute(string[] args, ExecuteType executeType, CancellationToken cancellationToken = default)
		{
			if (args.FirstOrDefault(p => p == "--version") != null)
			{
				Console.WriteLine("plang version: " + Assembly.GetAssembly(this.GetType())!.GetName().Version);
				return (null, null);
			}

			var result = await Run(args, cancellationToken);
			if (!result.Success && result.Error != null)
				return (result.Value, new Error(result.Error.Format()));
			return (result.Value, null);
		}

		public async Task<Runtime2.Engine.Memory.Data> Run(string[] args, CancellationToken cancellationToken = default)
		{
			// Normalize: "build" or "--build" both become the --build flag
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--build", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new Runtime2.Engine.@this(fileSystem);
			engine.SystemDirectory = fileSystem.SystemDirectory;

			// All CLI parameters go on the MemoryStack
			// --build → %!build%, --test → %!test%, --debug → %!debug%
			foreach (var param in parameters)
				engine.MemoryStack.Set(param.Key, param.Value);

			// Debug wiring
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
				engine.Debug.Apply(debugValue);

			// Build mode — set engine flag and resolve build path
			if (parameters.TryGetValue("!build", out var buildValue) && buildValue is not false)
			{
				engine.Building.IsEnabled = true;
				// Set %path% to the absolute path of the project being built
				if (!parameters.ContainsKey("path"))
					engine.MemoryStack.Set("path", fileSystem.RootDirectory);
			}

			// Set the goal file for the PLang runtime (only for non-build)
			if (!engine.Building.IsEnabled)
			{
				var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
				if (!prPath.StartsWith(".build"))
					prPath = ".build/" + prPath;
				engine.MemoryStack.Set("goalFile", "/" + prPath.ToLowerInvariant());
			}

			// Start the engine — reads system/.build/run.pr
			return await engine.Start();
		}
	}
}
