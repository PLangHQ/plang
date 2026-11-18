using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Data;

namespace PLang.Modules.DbModule
{
	/// <summary>
	/// One writer at a time per SQLite file, waited for with await instead of on a thread.
	///
	/// SQLite lets one connection write at a time. When a second one tries, Microsoft.Data.Sqlite
	/// retries with Thread.Sleep until the command timeout, and that sleeping thread is a thread
	/// pool thread. Measured on rafbokin at 100 requests a second (every page view writes a visit):
	/// 45 of 64 threads were asleep in SqliteDataReader.NextResult, the thread pool queue reached
	/// 2,881 items, and pages that do not write at all (/privacy, 4 ms) took 20 to 70 seconds.
	/// Waiting here costs no thread, and since writers no longer collide SQLite never has to retry.
	///
	/// Statements inside a transaction (begin transaction ... end transaction) are not gated:
	/// the transaction keeps the write lock between its statements, and a gate released after
	/// each statement would let another writer in to wait on that lock while holding the gate.
	/// </summary>
	internal static class SqliteWriteGate
	{
		private static readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);
		internal static int GateCount => gates.Count;

		public static SemaphoreSlim? For(IDbConnection? connection, IDbTransaction? transaction)
		{
			if (transaction != null) return null;
			if (connection is not SqliteConnection sqlite) return null;

			var cs = sqlite.ConnectionString ?? string.Empty;
			if (cs.Contains("Memory", StringComparison.OrdinalIgnoreCase)) return null;

			var file = sqlite.DataSource;
			if (string.IsNullOrEmpty(file)) return null;
			try { file = Path.GetFullPath(file); } catch { }

			return gates.GetOrAdd(file, _ => new SemaphoreSlim(1, 1));
		}
	}
}
