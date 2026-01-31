using LightInject;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Modules;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PLang.Runtime;

public class ModuleRegistry : IModuleRegistry
{
	// Pre-compiled regex for extracting short names
	private static readonly Regex ModuleNameRegex = new(@"PLang\.Modules\.(\w+)Module\.(Program|Builder)", RegexOptions.Compiled);

	private Dictionary<string, Type> _modules;
	private HashSet<string> _disabled;
	private HashSet<string> _removed;
	private readonly IServiceContainer _container;
	private readonly IPLangContextAccessor _contextAccessor;

	// Copy-on-write: track if we own the collections or are sharing with parent
	private bool _ownsCollections;
	// Reference to parent registry for copy-on-write
	private ModuleRegistry? _parent;

	public ModuleRegistry(IServiceContainer container, IPLangContextAccessor contextAccessor)
	{
		_container = container;
		_contextAccessor = contextAccessor;
		_modules = new(StringComparer.OrdinalIgnoreCase);
		_disabled = new(StringComparer.OrdinalIgnoreCase);
		_removed = new(StringComparer.OrdinalIgnoreCase);
		_ownsCollections = true;
	}

	// Private constructor for shallow clone (copy-on-write)
	private ModuleRegistry(IServiceContainer container, IPLangContextAccessor contextAccessor,
		Dictionary<string, Type> modules, HashSet<string> disabled, HashSet<string> removed)
	{
		_container = container;
		_contextAccessor = contextAccessor;
		_modules = modules;
		_disabled = disabled;
		_removed = removed;
		_ownsCollections = false; // We don't own these - will copy on first write
	}

	/// <summary>
	/// Ensures we have our own mutable copies of the collections (copy-on-write)
	/// </summary>
	private void EnsureWritable()
	{
		if (_ownsCollections) return;

		// Create our own copies
		_modules = new Dictionary<string, Type>(_modules, StringComparer.OrdinalIgnoreCase);
		_disabled = new HashSet<string>(_disabled, StringComparer.OrdinalIgnoreCase);
		_removed = new HashSet<string>(_removed, StringComparer.OrdinalIgnoreCase);
		_ownsCollections = true;
		_parent = null;
	}

	public void Register<T>() where T : BaseProgram
	{
		Register(typeof(T));
	}

	public void Register(Type moduleType)
	{
		var shortName = ExtractShortName(moduleType);
		Register(shortName, moduleType);
	}

	public void Register(string shortName, Type moduleType)
	{
		if (!typeof(BaseProgram).IsAssignableFrom(moduleType))
		{
			throw new ArgumentException($"Type {moduleType.FullName} must inherit from BaseProgram", nameof(moduleType));
		}

		EnsureWritable();
		_modules[shortName] = moduleType;
		_removed.Remove(shortName);
	}

	public void Remove<T>() where T : BaseProgram
	{
		Remove(ExtractShortName(typeof(T)));
	}

	public void Remove(string shortName)
	{
		EnsureWritable();
		_modules.Remove(shortName);
		_removed.Add(shortName);
		_disabled.Remove(shortName);
	}

	public void Disable(string shortName)
	{
		if (!_modules.ContainsKey(shortName) && !_removed.Contains(shortName))
		{
			throw new ArgumentException($"Module '{shortName}' is not registered", nameof(shortName));
		}
		EnsureWritable();
		_disabled.Add(shortName);
	}

	public void Enable(string shortName)
	{
		EnsureWritable();
		_disabled.Remove(shortName);
	}

	public bool IsEnabled(string shortName)
	{
		if (_removed.Contains(shortName)) return false;
		if (_disabled.Contains(shortName)) return false;
		return _modules.ContainsKey(shortName);
	}

	public (T? Module, IError? Error) Get<T>() where T : BaseProgram
	{
		var shortName = ExtractShortName(typeof(T));
		var (module, error) = Get(shortName);
		if (error != null) return (null, error);
		return ((T?)module, null);
	}

	public (BaseProgram? Module, IError? Error) Get(string shortName)
	{
		// Check if removed
		if (_removed.Contains(shortName))
		{
			return (null, new ProgramError($"Module '{shortName}' has been removed and is not available", Key: "ModuleRemoved"));
		}

		// Check if disabled
		if (_disabled.Contains(shortName))
		{
			return (null, new ProgramError($"Module '{shortName}' is disabled", Key: "ModuleDisabled"));
		}

		// Check if registered
		if (!_modules.TryGetValue(shortName, out var moduleType))
		{
			return (null, new ProgramError($"Module '{shortName}' is not registered", Key: "ModuleNotRegistered"));
		}

		// Create instance
		return CreateModuleInstance(moduleType);
	}

	public IReadOnlyList<string> GetRegisteredModules()
	{
		return _modules.Keys
			.Where(k => !_removed.Contains(k))
			.OrderBy(k => k)
			.ToList()
			.AsReadOnly();
	}

	public Type? GetModuleType(string shortName)
	{
		return _modules.TryGetValue(shortName, out var type) ? type : null;
	}

	private (BaseProgram? Module, IError? Error) CreateModuleInstance(Type moduleType)
	{
		try
		{
			var context = _contextAccessor.Current;
			var goal = context?.CallStack?.CurrentGoal ?? Goal.NotFound;
			var step = context?.CallStack?.CurrentStep;
			var instruction = step?.Instruction;

			// Get instance from container
			var program = _container.GetInstance(moduleType) as BaseProgram;
			if (program == null)
			{
				return (null, new ProgramError($"Could not create instance of {moduleType.FullName}", Key: "ModuleCreationFailed"));
			}

			// Initialize the program
			program.Init(_container, goal, step, instruction, _contextAccessor);

			return (program, null);
		}
		catch (Exception ex)
		{
			return (null, new ProgramError($"Error creating module {moduleType.FullName}: {ex.Message}", Key: "ModuleCreationException", Exception: ex));
		}
	}

	/// <summary>
	/// Extracts short name from module type.
	/// PLang.Modules.TerminalModule.Program -> "terminal"
	/// PLang.Modules.HttpModule.Program -> "http"
	/// </summary>
	public static string ExtractShortName(Type moduleType)
	{
		var fullName = moduleType.FullName ?? moduleType.Name;

		// Pattern: PLang.Modules.<Name>Module.Program or PLang.Modules.<Name>Module.Builder
		var match = ModuleNameRegex.Match(fullName);
		if (match.Success)
		{
			return match.Groups[1].Value.ToLowerInvariant();
		}

		// Fallback: just use the containing namespace's last part before "Module"
		var parts = fullName.Split('.');
		for (int i = parts.Length - 1; i >= 0; i--)
		{
			if (parts[i].EndsWith("Module", StringComparison.OrdinalIgnoreCase))
			{
				return parts[i].Substring(0, parts[i].Length - "Module".Length).ToLowerInvariant();
			}
		}

		// Last resort: use the type name
		return moduleType.Name.ToLowerInvariant();
	}

	/// <summary>
	/// Registers all modules found in the container that inherit from BaseProgram.
	/// </summary>
	public void RegisterAllFromContainer()
	{
		var currentAssembly = Assembly.GetExecutingAssembly();
		var moduleTypes = currentAssembly.GetTypes()
			.Where(t => !t.IsAbstract && !t.IsInterface && typeof(BaseProgram).IsAssignableFrom(t))
			.ToList();

		foreach (var moduleType in moduleTypes)
		{
			try
			{
				Register(moduleType);
			}
			catch
			{
				// Skip types that can't be registered
			}
		}
	}

	/// <summary>
	/// Creates a shallow clone of the registry using copy-on-write pattern.
	/// The clone shares the collections with the parent until a write operation occurs.
	/// This is very fast for the common case where modules are only read, not modified.
	/// </summary>
	public ModuleRegistry Clone()
	{
		// Use copy-on-write: just share the same collections
		// They will be copied only if the clone tries to modify them
		return new ModuleRegistry(_container, _contextAccessor, _modules, _disabled, _removed)
		{
			_parent = this
		};
	}
}
