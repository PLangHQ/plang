using System.Globalization;
using System.Text.RegularExpressions;

namespace PLang.Modules.HostModule;

public record DiskInfo(string Filesystem, string Mount, long SizeMb, long UsedMb, long AvailableMb, int UsedPercent);
public record MemoryInfo(long TotalMb, long UsedMb, long FreeMb, long AvailableMb, long SwapTotalMb, long SwapUsedMb);
public record ListeningPort(string Protocol, string Address, int Port, string? Process, int? Pid);
public record ProcessInfo(string User, int Pid, string Name, long CpuSeconds);

/// <summary>
/// The text of df, free, ss and ps turned into typed rows. Pure functions, no process is started here, so they can be tried
/// on the output of a real machine. A line that does not fit is left out, never guessed at.
/// </summary>
public static class HostParsers
{
	private static long L(string s) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

	/// <summary>df -P -m: Filesystem 1M-blocks Used Available Capacity Mounted on</summary>
	public static List<DiskInfo> ParseDf(string text)
	{
		var rows = new List<DiskInfo>();
		foreach (var line in Lines(text))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (p.Length < 6 || p[0] == "Filesystem" || !p[4].EndsWith('%')) continue;
			// A mount point may hold spaces: everything after the fifth column.
			string mount = string.Join(' ', p.Skip(5));
			int percent = (int)L(p[4].TrimEnd('%'));
			rows.Add(new DiskInfo(p[0], mount, L(p[1]), L(p[2]), L(p[3]), percent));
		}
		return rows;
	}

	/// <summary>free -m: the Mem: and the Swap: line.</summary>
	public static MemoryInfo? ParseFree(string text)
	{
		long total = 0, used = 0, free = 0, available = 0, swapTotal = 0, swapUsed = 0;
		bool sawMem = false;
		foreach (var line in Lines(text))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (p.Length >= 4 && p[0] == "Mem:")
			{
				sawMem = true;
				total = L(p[1]); used = L(p[2]); free = L(p[3]);
				// free's last column is "available" on current versions, older ones have no such column: then free + buffers is the best there is.
				available = p.Length >= 7 ? L(p[6]) : free;
			}
			else if (p.Length >= 3 && p[0] == "Swap:")
			{
				swapTotal = L(p[1]); swapUsed = L(p[2]);
			}
		}
		return sawMem ? new MemoryInfo(total, used, free, available, swapTotal, swapUsed) : null;
	}

	private static readonly Regex Users = new(@"users:\(\(""(?<name>[^""]*)"",pid=(?<pid>\d+)", RegexOptions.Compiled);

	/// <summary>ss -tulnpH: Netid State Recv-Q Send-Q Local-Address:Port Peer-Address:Port [Process]</summary>
	public static List<ListeningPort> ParseSs(string text)
	{
		var rows = new List<ListeningPort>();
		foreach (var line in Lines(text))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (p.Length < 5) continue;
			string protocol = p[0];
			if (protocol != "tcp" && protocol != "udp") continue;
			string local = p[4];
			int colon = local.LastIndexOf(':');
			if (colon < 1 || !int.TryParse(local[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)) continue;
			string address = local[..colon].Trim('[', ']');
			// 127.0.0.53%lo is an address on an interface: the interface is not part of the address.
			int percent = address.IndexOf('%');
			if (percent > 0) address = address[..percent];

			string? process = null;
			int? pid = null;
			var m = Users.Match(line);
			if (m.Success)
			{
				process = m.Groups["name"].Value;
				pid = int.Parse(m.Groups["pid"].Value, CultureInfo.InvariantCulture);
			}
			rows.Add(new ListeningPort(protocol, address, port, process, pid));
		}
		return rows;
	}

	/// <summary>ps -eo user:32,pid,comm:32,times --no-headers: user, pid, name (may hold spaces), cpu seconds.</summary>
	public static List<ProcessInfo> ParsePs(string text)
	{
		var rows = new List<ProcessInfo>();
		foreach (var line in Lines(text))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (p.Length < 4 || !int.TryParse(p[1], out int pid) || !long.TryParse(p[^1], out long seconds)) continue;
			rows.Add(new ProcessInfo(p[0], pid, string.Join(' ', p.Skip(2).Take(p.Length - 3)), seconds));
		}
		return rows;
	}

	private static IEnumerable<string> Lines(string text) =>
		(text ?? "").Split('\n').Select(l => l.TrimEnd('\r')).Where(l => !string.IsNullOrWhiteSpace(l));
}
