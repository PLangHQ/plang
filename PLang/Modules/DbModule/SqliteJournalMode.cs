using Microsoft.Data.Sqlite;
using System.Data;

namespace PLang.Modules.DbModule
{
	/// <summary>
	/// Setur SQLite grunna i WAL. Sjalfgefid er "delete", thar sem hver skrifari lokar a alla
	/// lesara. Maelt a rafbokin-gognum, 6 lesarar + 3 skrifarar i 12s: 131 lestur og 11
	/// laesingarvillur i delete, 157.464 lestrar og 0 laesingarvillur i WAL.
	///
	/// Hamurinn er varanlegur i skranni, svo thetta er no-op eftir fyrsta skipti.
	/// </summary>
	internal static class SqliteJournalMode
	{
		private static readonly string[] synchronousNormalPaths = (Environment.GetEnvironmentVariable("PLANG_SQLITE_SYNCHRONOUS_NORMAL") ?? "")
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(p => p.Replace('\\', '/'))
			.ToArray();

		private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> walConfirmed = new(StringComparer.OrdinalIgnoreCase);

		private static bool IsSynchronousNormal(string? file)
		{
			if (synchronousNormalPaths.Length == 0 || string.IsNullOrEmpty(file)) return false;
			var path = file.Replace('\\', '/');
			return synchronousNormalPaths.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Ma EKKI keyra inni i faerslu. Kalladu strax a eftir Open() og A UNDAN BeginTransaction().
		/// </summary>
		public static void EnableWal(IDbConnection? connection)
		{
			if (connection is not SqliteConnection sqlite) return;
			if (sqlite.State != ConnectionState.Open) return;

			// Minnisgrunnar hafa enga skra til ad skrifa WAL i og eru hvort ed er einir um sig.
			var cs = sqlite.ConnectionString ?? string.Empty;
			if (cs.Contains("Memory", StringComparison.OrdinalIgnoreCase)) return;

			// The journal mode is stored in the database file, so once a file has answered "wal" the PRAGMA
			// is not repeated for it in this process (it ran on every connection of every database step).
			// synchronous is per connection and is still set every time for the files that ask for it.
			var file = sqlite.DataSource;
			bool needsSynchronous = IsSynchronousNormal(file);
			if (!needsSynchronous && !string.IsNullOrEmpty(file) && walConfirmed.ContainsKey(file)) return;

			try
			{
				using var command = sqlite.CreateCommand();
				command.CommandText = "PRAGMA journal_mode=WAL;";
				var mode = command.ExecuteScalar() as string;
				if (!string.IsNullOrEmpty(file) && string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase)) walConfirmed.TryAdd(file, 0);

				// synchronous=FULL (the default) waits for an fsync on every commit: 8 ms here, so one
				// SQLite file takes at most about 120 commits a second, and every page view on rafbokin
				// commits a visit. In WAL, NORMAL syncs at checkpoints instead; a crash of the app loses
				// nothing, a power loss or OS crash can lose the last commits but never corrupts the file.
				// That is fine for analytics, not for orders or payments, so it is only used for the
				// files named in PLANG_SQLITE_SYNCHRONOUS_NORMAL: a comma separated list of path parts,
				// e.g. ".db/analytics/". Unset (the default) leaves every file on FULL.
				// Per connection, so it is set every time a connection is opened.
				if (string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase) && IsSynchronousNormal(sqlite.DataSource))
				{
					command.CommandText = "PRAGMA synchronous=NORMAL;";
					command.ExecuteNonQuery();
				}
			}
			catch (Exception)
			{
				// Se skrain laest eda opnud skrifvarin skilar SQLite gamla hamnum i stad thess ad
				// kasta, en badir endar eru meinlausir: grunnurinn heldur afram i delete.
				// Thetta ma aldrei fella beidnina sem var ad opna tenginguna.
			}
		}
	}
}
