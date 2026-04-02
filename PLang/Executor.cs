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

			if (executeType == ExecuteType.Builder)
			{
				var buildResult = await Build(args);
				return (buildResult.Variables, buildResult.Error);
			}

			var result = await Run(args, cancellationToken);
			return (result.Value, null);
		}

		public async Task<(object? Engine, object? Variables, IError? Error)> Build(string[] args)
		{
			var (_, parameters) = CommandLineParser.Parse(args);

			// Create v2 engine rooted at the user's project directory
			var engine2 = new Runtime2.Engine.@this(fileSystem);
			// SystemDirectory points to the system/ folder next to plang.exe
			engine2.SystemDirectory = fileSystem.SystemDirectory;
			engine2.Building.IsEnabled = true;

			// Debug: --debug=true or --debug={"goal":"X","step":3}
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
			{
				engine2.Debug.Apply(debugValue);
			}
			parameters.Remove("!debug");

			// Resolve build path relative to user's project root
			if (!parameters.TryGetValue("path", out var pathValue) || pathValue is not string pathStr)
				pathStr = ".";
			parameters["path"] = System.IO.Path.GetFullPath(System.IO.Path.Join(fileSystem.RootDirectory, pathStr));

			// Parameters already have ! prefix for system params (--build → !build)
			foreach (var param in parameters)
				engine2.MemoryStack.Set(param.Key, param.Value);

			// Run /system/Build which calls /system/builder/Build → BuildGoal → ApplyStep etc.
			// Absolute path so user can override by placing system/ in their app folder.
			var result = await engine2.RunGoalAsync(
				new Runtime2.Engine.Goals.Goal.GoalCall { Name = "/system/Build" });

			if (!result.Success)
			{
				return (null, null, result.Error != null
					? new Error(result.Error.Format())
					: new Error("Build failed"));
			}
			return (null, result.Value, null);
		}

		public async Task<Runtime2.Engine.Memory.Data> Run(string[] args, CancellationToken cancellationToken = default)
		{
			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new Runtime2.Engine.@this(fileSystem);
			engine.SystemDirectory = fileSystem.SystemDirectory;

			// All CLI parameters go on the MemoryStack
			// --build → %!build%, --test → %!test%, --debug → %!debug%
			foreach (var param in parameters)
				engine.MemoryStack.Set(param.Key, param.Value);

			// Debug wiring (reads from %!debug% on MemoryStack)
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
				engine.Debug.Apply(debugValue);

			// Set the goal file for the PLang runtime
			var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
			if (!prPath.StartsWith(".build"))
				prPath = ".build/" + prPath;
			engine.MemoryStack.Set("goalFile", "/" + prPath.ToLowerInvariant());

			// Start the engine — reads system/.build/run.pr, kernel-loops its steps
			return await engine.Start();
		}
	}
}
