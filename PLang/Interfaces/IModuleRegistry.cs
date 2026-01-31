using PLang.Errors;
using PLang.Modules;

namespace PLang.Interfaces;

public interface IModuleRegistry
{
	// Registration
	void Register<T>() where T : BaseProgram;
	void Register(Type moduleType);
	void Register(string shortName, Type moduleType);

	// Removal (security control)
	void Remove<T>() where T : BaseProgram;
	void Remove(string shortName);

	// Enable/Disable (security control)
	void Disable(string shortName);
	void Enable(string shortName);
	bool IsEnabled(string shortName);

	// Access - returns (module, error) tuple for proper error handling
	(T? Module, IError? Error) Get<T>() where T : BaseProgram;
	(BaseProgram? Module, IError? Error) Get(string shortName);

	// Discovery
	IReadOnlyList<string> GetRegisteredModules();
	Type? GetModuleType(string shortName);

	// Initialization
	void RegisterAllFromContainer();
}
