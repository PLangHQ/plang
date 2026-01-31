using PLang.Interfaces;

namespace PLang.Runtime;

/// <summary>
/// Manages a pool of engines for concurrent request handling.
/// Creates engines on demand - first Rent() creates the first engine.
/// </summary>
public interface IEnginePool : IDisposable
{
	/// <summary>
	/// Rent an engine from the pool. Creates a new one if pool is empty.
	/// The engine is initialized with a fresh context ready for use.
	/// </summary>
	/// <param name="parentEngine">Optional parent engine for faster child creation (reuses PrParser, output sinks, etc.)</param>
	IEngine Rent(IEngine? parentEngine = null);

	/// <summary>
	/// Return an engine to the pool for reuse.
	/// </summary>
	void Return(IEngine engine);

	/// <summary>
	/// Pre-warm the pool by creating engines in the background.
	/// Call this after the main engine is initialized.
	/// </summary>
	/// <param name="parentEngine">Parent engine for faster child creation</param>
	/// <param name="count">Number of engines to pre-create</param>
	void PreWarm(IEngine parentEngine, int count = 5);

	/// <summary>
	/// Current number of engines in the pool (available for rent).
	/// </summary>
	int AvailableCount { get; }

	/// <summary>
	/// Total engines created by this pool.
	/// </summary>
	int TotalCreated { get; }
}
