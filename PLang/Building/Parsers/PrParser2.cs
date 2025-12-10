using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.PlangModule.Data;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Export;
using static PLang.Runtime.Startup.ModuleLoader;
using static PLang.Utils.VariableHelper;

namespace PLang.Building.Parsers;

public class PrParser2
{
	private IReadOnlyList<PrGoal> PrGoals = null!;

	ConcurrentDictionary<string, List<PrGoal>> Events = new();
	ConcurrentDictionary<string, List<PrGoal>> SystemEvents = new();
	private readonly Dictionary<string, Instruction> instructions = new Dictionary<string, Instruction>();
	private readonly IPLangFileSystem fileSystem;
	private readonly ILogger logger;
	private bool disposed;

	public PrParser2(IPLangFileSystem fileSystem, ILogger logger)
	{
		this.fileSystem = fileSystem;
		this.logger = logger;

		

	}
	/*
	public PrGoal? GetEvent(string name)
	{
		var events = GetEvents(name);
		return events.FirstOrDefault();
	}
	public List<PrGoal> GetEvents(string name)
	{
		if (Events.TryGetValue(name, out var eventPrGoals)) return eventPrGoals;

		eventPrGoals = new();
		if (PrGoals == null) return eventPrGoals;
		eventPrGoals = PrGoals.Where(p => p.PrGoalName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
		Events.TryAdd(name, eventPrGoals);
		return eventPrGoals;
	}

	public PrGoal? GetSystemEvent(string name)
	{
		var events = GetSystemEvents(name);
		return events.FirstOrDefault();
	}
	public List<PrGoal> GetSystemEvents(string name)
	{
		if (SystemEvents.TryGetValue(name, out var eventPrGoals)) return eventPrGoals;

		eventPrGoals = new();
		if (PrGoals == null) return eventPrGoals;
		eventPrGoals = systemPrGoals.Where(p => p.PrGoalName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
		SystemEvents.TryAdd(name, eventPrGoals);
		return eventPrGoals;
	}

	public virtual PrGoal? ParsePrFile(string absolutePrFilePath)
	{
		if (!absolutePrFilePath.Contains(".pr"))
		{
			throw new ArgumentException($"path ({absolutePrFilePath} does not contain .pr file");
		}

		var PrGoal = JsonHelper.ParseFilePath<PrGoal>(fileSystem, absolutePrFilePath);
		if (PrGoal == null)
		{
			return null;
		}
		var appAbsoluteStartupPath = fileSystem.RootDirectory;
		if (!absolutePrFilePath.StartsWith(fileSystem.RootDirectory))
		{
			appAbsoluteStartupPath = absolutePrFilePath.Substring(0, absolutePrFilePath.IndexOf(".build"));
		}

		var appsPath = absolutePrFilePath.Replace(appAbsoluteStartupPath, "");
		if (appsPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + "apps" + fileSystem.Path.DirectorySeparatorChar))
		{
			var paths = appsPath.Split(fileSystem.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
			appsPath = fileSystem.Path.DirectorySeparatorChar + paths[0] + fileSystem.Path.DirectorySeparatorChar + paths[1];
			PrGoal.AppName = paths[1];

			PrGoal.RelativeAppStartupFolderPath = appsPath;
			PrGoal.RelativePrGoalFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrGoalFolderPath));
			PrGoal.RelativePrGoalPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrGoalPath));
			PrGoal.RelativePrPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrPath));
			PrGoal.RelativePrFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrFolderPath));
			PrGoal.AbsoluteAppStartupFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appAbsoluteStartupPath, appsPath));

		}
		else if (appsPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + ".services" + fileSystem.Path.DirectorySeparatorChar))
		{
			int i = 0;
			var paths = appsPath.Split(fileSystem.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
			appsPath = fileSystem.Path.DirectorySeparatorChar + paths[0] + fileSystem.Path.DirectorySeparatorChar + paths[1];
			PrGoal.AppName = paths[1];

			PrGoal.RelativeAppStartupFolderPath = appsPath;
			PrGoal.RelativePrGoalFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrGoalFolderPath));
			PrGoal.RelativePrGoalPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrGoalPath));
			PrGoal.RelativePrPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrPath));
			PrGoal.RelativePrFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, PrGoal.RelativePrFolderPath));
			PrGoal.AbsoluteAppStartupFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appAbsoluteStartupPath, appsPath));
		}
		else
		{
			PrGoal.AppName = fileSystem.Path.DirectorySeparatorChar.ToString();

			PrGoal.AbsoluteAppStartupFolderPath = appAbsoluteStartupPath;
			PrGoal.RelativeAppStartupFolderPath = fileSystem.Path.DirectorySeparatorChar.ToString();
		}


		PrGoal.AbsolutePrGoalPath = fileSystem.Path.Join(appAbsoluteStartupPath, PrGoal.RelativePrGoalPath);
		PrGoal.AbsolutePrGoalFolderPath = fileSystem.Path.Join(appAbsoluteStartupPath, PrGoal.RelativePrGoalFolderPath);

		PrGoal.AbsolutePrFilePath = fileSystem.Path.Join(appAbsoluteStartupPath, PrGoal.RelativePrPath);
		PrGoal.AbsolutePrFolderPath = fileSystem.Path.Join(appAbsoluteStartupPath, PrGoal.RelativePrFolderPath);

		AdjustPathsToOS(PrGoal);
		PrGoal.IsSystem = absolutePrFilePath.Contains(fileSystem.SystemDirectory);

	
		PrGoal.Description = null;
		if (PrGoal.BuilderVersion != null)
		{
			PrGoal.BuilderVersion = string.IsInterned(PrGoal.BuilderVersion) ?? string.Intern(PrGoal.BuilderVersion);
		}


		for (int i = 0; i < PrGoal.PrGoalSteps.Count; i++)
		{
			PrGoal.PrGoalSteps[i].AbsolutePrFilePath = fileSystem.Path.Join(PrGoal.AbsolutePrFolderPath, PrGoal.PrGoalSteps[i].PrFileName).AdjustPathToOs();
			PrGoal.PrGoalSteps[i].RelativePrPath = fileSystem.Path.Join(PrGoal.RelativePrFolderPath, PrGoal.PrGoalSteps[i].PrFileName).AdjustPathToOs();
			PrGoal.PrGoalSteps[i].AppStartupPath = appAbsoluteStartupPath.AdjustPathToOs();
			PrGoal.PrGoalSteps[i].Number = i;
			PrGoal.PrGoalSteps[i].Index = i;
			

			//remove from memory uneeded data for runtime
			PrGoal.PrGoalSteps[i].PrGoal = PrGoal;
			PrGoal.PrGoalSteps[i].LlmRequest = null;
			PrGoal.PrGoalSteps[i].PrFile = null;
			PrGoal.PrGoalSteps[i].Description = null;
			PrGoal.PrGoalSteps[i].UserIntent = null;
			PrGoal.PrGoalSteps[i].Confidence = null;

			// testing to reduce memory of repeated strings
			if (PrGoal.PrGoalSteps[i].ModuleType != null)
			{
				PrGoal.PrGoalSteps[i].ModuleType = string.IsInterned(PrGoal.PrGoalSteps[i].ModuleType) ?? string.Intern(PrGoal.PrGoalSteps[i].ModuleType);
			}
			if (PrGoal.PrGoalSteps[i].BuilderVersion != null)
			{
				PrGoal.PrGoalSteps[i].BuilderVersion = string.IsInterned(PrGoal.PrGoalSteps[i].BuilderVersion) ?? string.Intern(PrGoal.PrGoalSteps[i].BuilderVersion);
			}
			if (PrGoal.PrGoalSteps[i].PrFileName != null)
			{
				PrGoal.PrGoalSteps[i].PrFileName = string.IsInterned(PrGoal.PrGoalSteps[i].PrFileName) ?? string.Intern(PrGoal.PrGoalSteps[i].PrFileName);
			}
		}

		return PrGoal;
	}

	protected virtual void ThrowIfDisposed()
	{
		if (this.disposed)
		{
			throw new ObjectDisposedException(this.GetType().FullName);
		}
	}

	private static void AdjustPathsToOS(PrGoal PrGoal)
	{
		PrGoal.RelativeAppStartupFolderPath = PrGoal.RelativeAppStartupFolderPath.AdjustPathToOs();
		PrGoal.RelativePrGoalFolderPath = PrGoal.RelativePrGoalFolderPath.AdjustPathToOs();
		PrGoal.RelativePrGoalPath = PrGoal.RelativePrGoalPath.AdjustPathToOs();
		PrGoal.RelativePrPath = PrGoal.RelativePrPath.AdjustPathToOs();
		PrGoal.RelativePrFolderPath = PrGoal.RelativePrFolderPath.AdjustPathToOs();

		PrGoal.AbsoluteAppStartupFolderPath = PrGoal.AbsoluteAppStartupFolderPath.AdjustPathToOs();
		PrGoal.AbsolutePrGoalPath = PrGoal.AbsolutePrGoalPath.AdjustPathToOs();
		PrGoal.AbsolutePrGoalFolderPath = PrGoal.AbsolutePrGoalFolderPath.AdjustPathToOs();
		PrGoal.AbsolutePrFilePath = PrGoal.AbsolutePrFilePath.AdjustPathToOs();
		PrGoal.AbsolutePrFolderPath = PrGoal.AbsolutePrFolderPath.AdjustPathToOs();
	}

	public Instruction? ParseInstructionFile(PrGoalStep step)
	{
		if (instructions.TryGetValue(step.AbsolutePrFilePath, out var instruction))
		{
			return instruction;
		}

		instruction = JsonHelper.ParseFilePath<Instruction>(fileSystem, step.AbsolutePrFilePath);
		if (instruction != null)
		{
			instruction.LlmRequest = null;
			instructions.TryAdd(step.AbsolutePrFilePath, instruction);
		}
		
		return instruction;

	}
	public IReadOnlyList<PrGoal> ForceLoadAllPrGoals()
	{
		var PrGoals = LoadAllPrGoals(true);
		return PrGoals;
	}


	public IReadOnlyList<PrGoal> LoadAllPrGoals(bool force = false)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		logger.LogDebug($"   -- Loading all PrGoals(force:{force})");

		GetPrGoals(force);
		GetSystemPrGoals(force);

		if (force)
		{
			instructions.Clear();
		}

		logger.LogDebug($"   -- Done loading all PrGoals - {stopwatch.ElapsedMilliseconds}");
		return PrGoals;
	}

	public List<PrGoal> LoadAllPrGoalsByPath(string dir)
	{

		string buildDir = fileSystem.Path.Join(dir, ".build");
		if (!fileSystem.Directory.Exists(buildDir))
		{
			return new List<PrGoal>();
		}

		var files = fileSystem.Directory.GetFiles(buildDir, ISettings.PrGoalFileName, SearchOption.AllDirectories).ToList();
		files = files.Select(file => new
		{
			FileName = file,
			Order = file.ToLower().EndsWith(@"events\events\00. PrGoal.pr") ? 0 :
				file.ToLower().Contains(@"events\") ? 1 :
				file.ToLower().Contains(@"setup\") ? 2 :
				file.ToLower().Contains(@"start\") ? 3 : 4
		}).OrderBy(file => file.Order)
			.ThenBy(file => file.FileName)
			.Select(file => file.FileName).ToList();


		var PrGoals = new List<PrGoal>();
		foreach (var file in files)
		{
			var PrGoal = ParsePrFile(file);
			if (PrGoal != null)
			{
				PrGoals.Add(PrGoal);
			}
		}

		return PrGoals;
	}

	public List<PrGoal> LoadAppsByPath(string dir)
	{

		string appsDir = fileSystem.Path.Join(dir, "apps");
		List<string> files = new();
		if (fileSystem.Directory.Exists(appsDir))
		{
			var unsortedFiles = fileSystem.Directory.GetFiles(appsDir, ISettings.PrGoalFileName, SearchOption.AllDirectories).ToList();
			unsortedFiles = unsortedFiles.Select(file => new
			{
				FileName = file,
				Order = file.ToLower().EndsWith(@"events\events\00. PrGoal.pr") ? 0 :
				file.ToLower().Contains(@"events\") ? 1 :
				file.ToLower().Contains(@"setup\") ? 2 :
				file.ToLower().Contains(@"start\") ? 3 : 4
			})
				.OrderBy(file => file.Order)
				.ThenBy(file => file.FileName)
				.Select(file => file.FileName).ToList();
			files.AddRange(unsortedFiles);
		}

		var PrGoals = new List<PrGoal>();
		foreach (var file in files)
		{
			var PrGoal = ParsePrFile(file);
			if (PrGoal != null)
			{
				PrGoals.Add(PrGoal);
			}
		}
		return PrGoals;
	}
	public IReadOnlyList<PrGoal> GetAllPrGoals()
	{
		if (PrGoals.Count > 0) return PrGoals;

		LoadAllPrGoals();
		return PrGoals;
	}

	public IReadOnlyList<PrGoal> GetPublicPrGoals()
	{
		if (publicPrGoals.Count > 0) return publicPrGoals;
		LoadAllPrGoals();
		return publicPrGoals;
	}

	public (PrGoal? PrGoal, IError? Error) GetPrGoal(PrGoalToCallInfo PrGoalToCall)
	{
		var PrGoals = GetAllPrGoals();
		if (!string.IsNullOrEmpty(PrGoalToCall.Path))
		{
			var PrGoal = PrGoals.FirstOrDefault(p => p.RelativePrPath.Equals(PrGoalToCall.Path));
			if (PrGoal == null)
			{
				var systemPrGoals = GetSystemPrGoals();
				PrGoal = systemPrGoals.FirstOrDefault(p => p.RelativePrPath.Equals(PrGoalToCall.Path));
				if (PrGoal == null)
				{
					return (null, new NotFoundError($"PrGoal {PrGoalToCall.Name} could not be found. Search at {PrGoalToCall.Path}", "PrGoalNotFound"));
				}
			}
			return (PrGoal, null);
		}

		var PrGoalsFound = PrGoals.Where(p => p.PrGoalName.Equals(PrGoalToCall.Name, StringComparison.OrdinalIgnoreCase));
		if (PrGoalsFound.Count() > 1)
		{
			string fixSuggestion = $"Rename one of these PrGoals:\n\t{string.Join("\n", PrGoalsFound.Select(p => p.RelativePrGoalPath))}";
			return (null, new Error($"Found more the one PrGoal with the name {PrGoalToCall.Name}. Cannot decide which to use.",
				FixSuggestion: fixSuggestion));
		}
		if (PrGoalsFound.Any())
		{
			return (PrGoalsFound.First(), null);
		}
		return (null, new NotFoundError($"PrGoal {PrGoalToCall.Name} could not be found.", "PrGoalNotFound"));
	}

	public PrGoal? GetPrGoal(string absolutePrFilePath)
	{
		return ParsePrFile(absolutePrFilePath);
	}

	public PrGoal? GetPrGoalByAppAndPrGoalName(string appStartupPath, string PrGoalNameOrPath, PrGoal? callingPrGoal = null)
	{
		if (string.IsNullOrEmpty(appStartupPath))
		{
			throw new ArgumentNullException(nameof(appStartupPath));
		}
		if (string.IsNullOrEmpty(PrGoalNameOrPath))
		{
			throw new ArgumentNullException(nameof(PrGoalNameOrPath));
		}
		
		appStartupPath = appStartupPath.AdjustPathToOs();
		if (appStartupPath == fileSystem.Path.DirectorySeparatorChar.ToString())
		{
			appStartupPath = fileSystem.RootDirectory;
		}
		PrGoalNameOrPath = PrGoalNameOrPath.AdjustPathToOs().Replace(".PrGoal", "").Replace("!", "");

		if (appStartupPath != fileSystem.RootDirectory && !fileSystem.IsPlangRooted(appStartupPath))
		{
			appStartupPath = appStartupPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
			if (!appStartupPath.StartsWith(fileSystem.Path.DirectorySeparatorChar.ToString()))
			{
				appStartupPath = fileSystem.Path.DirectorySeparatorChar.ToString() + appStartupPath;
			}
		}

		PrGoal? PrGoal = null;

		// first check for PrGoal inside same PrGoal file as the calling PrGoal
		if (callingPrGoal != null && !PrGoalNameOrPath.Contains(fileSystem.Path.DirectorySeparatorChar))
		{
			PrGoal = PrGoals.FirstOrDefault(p => p.RelativePrGoalFolderPath == callingPrGoal.RelativePrGoalFolderPath && p.PrGoalName.Equals(PrGoalNameOrPath, StringComparison.OrdinalIgnoreCase));
			if (PrGoal != null) return PrGoal;
		}

		// match PrGoal from root, e.g. /Start
		if (PrGoalNameOrPath.StartsWith(fileSystem.Path.DirectorySeparatorChar))
		{
			PrGoal = PrGoals.FirstOrDefault(p => p.RelativePrFolderPath.Equals(fileSystem.Path.Join(".build", PrGoalNameOrPath), StringComparison.OrdinalIgnoreCase));
			if (PrGoal != null) return PrGoal;
		}

		// match PrGoal from calling PrGoal, e.g. calling PrGoal is in /ui/ folder, when PrGoalNameOrPath is user/edit, it matches /ui/user/edit.PrGoal
		if (callingPrGoal != null && !PrGoalNameOrPath.StartsWith(fileSystem.Path.DirectorySeparatorChar))
		{
			var newPrGoalPath = fileSystem.Path.Join(".build", callingPrGoal.RelativePrGoalFolderPath, PrGoalNameOrPath);
			PrGoal = PrGoals.FirstOrDefault(p => p.RelativePrFolderPath.Equals(newPrGoalPath, StringComparison.OrdinalIgnoreCase));
			if (PrGoal != null) return PrGoal;
		}

		PrGoal = PrGoals.FirstOrDefault(p => p.RelativePrFolderPath.Equals(fileSystem.Path.Join(".build", PrGoalNameOrPath), StringComparison.OrdinalIgnoreCase));
		if (PrGoal != null) return PrGoal;

		PrGoal = PrGoals.FirstOrDefault(p => PrGoalNameOrPath.TrimStart(fileSystem.Path.DirectorySeparatorChar).Equals(fileSystem.Path.Join(p.RelativePrGoalFolderPath, p.PrGoalName).TrimStart(fileSystem.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
		if (PrGoal != null) return PrGoal;


		var possiblePrGoals = PrGoals.Where(p => p.RelativePrFolderPath.EndsWith(PrGoalNameOrPath, StringComparison.OrdinalIgnoreCase)).ToList();
		if (possiblePrGoals.Count == 1) return possiblePrGoals[0];
		if (possiblePrGoals.Count > 1)
		{
			var PrGoalNames = possiblePrGoals.Select(p =>
			{
				return p.RelativePrGoalPath;
			});
			throw new PrGoalNotFoundException($"{PrGoalNameOrPath} Could not be found. There are {possiblePrGoals.Count} to choose from. {string.Join(",", PrGoalNames)}", appStartupPath, PrGoalNameOrPath);
		}

		return PrGoal;
	}

	public IReadOnlyList<PrGoal> GetPrGoals(bool force = false)
	{
		if (!force && PrGoals != null) return PrGoals;

		PrGoals = LoadAllPrGoalsByPath(fileSystem.RootDirectory);
		publicPrGoals = PrGoals.Where(p => p.Visibility == Visibility.Public).ToList();


		return PrGoals;
	}

	public IReadOnlyList<PrGoal> GetSystemPrGoals(bool force = false)
	{
		if (!force && systemPrGoals != null) return systemPrGoals;

		systemPrGoals = LoadAllPrGoalsByPath(fileSystem.SystemDirectory);

		return systemPrGoals;
	}
	public List<PrGoal> GetApps()
	{
		var groupedPrGoals = GetAllPrGoals().GroupBy(p => p.AppName);
		var PrGoals = new List<PrGoal>();
		foreach (var groupedPrGoal in groupedPrGoals)
		{
			var PrGoal = groupedPrGoal.FirstOrDefault();
			if (PrGoal != null && PrGoal.RelativeAppStartupFolderPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + "apps"))
			{
				PrGoals.Add(PrGoal);
			}
		}
		return PrGoals;
	}

	public async Task<(List<PrGoal>? PrGoals, IError? Error)> LoadAppPath(string appName, IFileAccessHandler fileAccessHandler)
	{
		var path = fileSystem.PrGoalsPath;
		var appPath = fileSystem.Path.Join(path, appName, ".build");

		List<string>? files = new();

		if (fileSystem.Directory.Exists(appPath))
		{
			files = fileSystem.Directory.GetFiles(fileSystem.Path.Join(appPath, ".build"), ISettings.PrGoalFileName, SearchOption.AllDirectories).ToList();
		}

		if (files.Count == 0)
		{
			appPath = fileSystem.Path.Join(fileSystem.SystemDirectory, appName, ".build");
			if (fileSystem.Directory.Exists(appPath))
			{
				files = fileSystem.Directory.GetFiles(appPath, ISettings.PrGoalFileName, SearchOption.AllDirectories).ToList();
			}
		}
		var PrGoals = new List<PrGoal>();
		foreach (var file in files)
		{
			var PrGoal = ParsePrFile(file);
			if (PrGoal != null)
			{
				PrGoals.Add(PrGoal);
			}
		}
		if (PrGoals.Count == 0) return (null, new Error($"App '{appName}' could not be found", "AppNotFound"));
		return (PrGoals, null);
	}


	public (List<Instruction>? Instructions, IError? Error) GetInstructions(IReadOnlyList<PrGoalStep> steps, string? functionName = null)
	{
		var instructions = new List<Instruction>();
		foreach (var step in steps)
		{
			var instruction = ParseInstructionFile(step);
			if (instruction == null) continue;

			if (instruction.Function.Name != functionName) continue;

			instructions.Add(instruction);
		}

		return (instructions, null);

	}

	public IReadOnlyList<PrGoal> GetEventsFiles(bool builder = false)
	{
		if (builderEventsPrGoals != null && builder) return builderEventsPrGoals;
		if (runtimeEventsPrGoals != null && !builder) return runtimeEventsPrGoals;

		if (!fileSystem.Directory.Exists(fileSystem.BuildPath))
		{
			return [];
		}

		List<PrGoal> eventFiles = new();
		string eventsFolderName = (!builder) ? "Events" : "BuilderEvents";

		var eventsFolderPath = fileSystem.Path.Join(fileSystem.BuildPath, "events", eventsFolderName);
		var rootEventFilePath = fileSystem.Path.Join(eventsFolderPath, "00. PrGoal.pr");
		if (fileSystem.File.Exists(rootEventFilePath))
		{
			var PrGoal = this.ParsePrFile(rootEventFilePath);
			if (PrGoal != null)
			{
				eventFiles.Add(PrGoal);
			}
		}
		//todo: hack, otherwise it will load events twice when building /system/
		if (fileSystem.BuildPath.EndsWith("/plang/system/.build".AdjustPathToOs())) return eventFiles;

		var osEventsPath = fileSystem.Path.Join(fileSystem.SystemDirectory, ".build", "events", eventsFolderName);
		var osEventFilePath = fileSystem.Path.Join(osEventsPath, "00. PrGoal.pr");
		if (fileSystem.File.Exists(osEventFilePath))
		{
			var PrGoal = this.ParsePrFile(osEventFilePath);
			if (PrGoal != null)
			{
				eventFiles.Add(PrGoal);
			}
		}

		return eventFiles;

	}

	internal void ClearVariables()
	{
		for (int i = 0; i < PrGoals.Count; i++)
		{
			for (int b = 0; b < PrGoals[i].Variables.Count; b++)
			{
				if (PrGoals[i].Variables[b].DisposeFunc != null)
				{
					PrGoals[i].Variables[b]?.DisposeFunc()?.Wait();
				}
				
			}
			
			PrGoals[i].Variables.Clear();
			PrGoals[i].Variables = new();
			for (int b = 0; b < PrGoals[i].PrGoalSteps.Count; b++)
			{
				for (int c = 0; c < PrGoals[i].PrGoalSteps[b].Variables.Count; c++)
				{
					if (PrGoals[i].PrGoalSteps[b].Variables[c]?.DisposeFunc != null)
					{
						PrGoals[i].PrGoalSteps[b].Variables[c]?.DisposeFunc()?.Wait();
					}
				}

				PrGoals[i].PrGoalSteps[b].Variables.Clear();
				PrGoals[i].PrGoalSteps[b].Variables = new();
			}
		}

	}*/
}
