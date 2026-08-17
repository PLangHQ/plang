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

			try
			{
				using var command = sqlite.CreateCommand();
				command.CommandText = "PRAGMA journal_mode=WAL;";
				command.ExecuteScalar();
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
