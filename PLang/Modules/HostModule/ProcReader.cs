using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace PLang.Modules.HostModule;

/// <summary>
/// The machine as the kernel states it, read from /proc: memory, processes, listening sockets and mounted disks. No program is started,
/// so a program a hacked root has replaced (ss, ps, free) cannot lie about it. The root of /proc is a parameter so the readers can be tried on a made-up tree.
/// A line or a process that does not read (it ended while being read, no permission) is left out, never guessed at.
/// </summary>
public static class ProcReader
{
	// The kernel counts CPU time in ticks; 100 per second is what every Linux that plang runs on uses (sysconf(_SC_CLK_TCK) is not reachable from .NET).
	private const int ClockTicksPerSecond = 100;

	private static long Num(string s) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

	// ---- memory: /proc/meminfo, in MB the way `free -m` counts them (KiB / 1024, cut)

	public static MemoryInfo? ReadMemory(string procRoot = "/proc")
	{
		string file = Path.Join(procRoot, "meminfo");
		if (!File.Exists(file)) return null;

		var kb = new Dictionary<string, long>();
		foreach (var line in File.ReadLines(file))
		{
			int colon = line.IndexOf(':');
			if (colon < 1) continue;
			var parts = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length > 0) kb[line[..colon]] = Num(parts[0]);
		}
		if (!kb.ContainsKey("MemTotal")) return null;

		long total = kb["MemTotal"], free = kb.GetValueOrDefault("MemFree");
		long available = kb.GetValueOrDefault("MemAvailable", free);
		// procps' "used": what is neither free nor cache that the kernel can give back.
		long cache = kb.GetValueOrDefault("Buffers") + kb.GetValueOrDefault("Cached") + kb.GetValueOrDefault("SReclaimable");
		long used = Math.Max(0, total - free - cache);
		long swapTotal = kb.GetValueOrDefault("SwapTotal"), swapFree = kb.GetValueOrDefault("SwapFree");
		return new MemoryInfo(total / 1024, used / 1024, free / 1024, available / 1024, swapTotal / 1024, (swapTotal - swapFree) / 1024);
	}

	// ---- processes: /proc/<pid>/stat (name, cpu time) and /proc/<pid>/status (owner)

	public static List<ProcessInfo> ReadProcesses(string procRoot = "/proc", string passwdFile = "/etc/passwd")
	{
		var users = ReadUsers(passwdFile);
		var rows = new List<ProcessInfo>();
		foreach (var dir in Directory.EnumerateDirectories(procRoot))
		{
			if (!int.TryParse(Path.GetFileName(dir), out int pid)) continue;
			try
			{
				string stat = File.ReadAllText(Path.Join(dir, "stat"));
				// "pid (name) state ...": the name may hold spaces and brackets, so cut at the LAST ")".
				int open = stat.IndexOf('('), close = stat.LastIndexOf(')');
				if (open < 0 || close < open) continue;
				string name = stat[(open + 1)..close];
				var rest = stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
				// after the name: state(0) ppid(1) ... utime(11) stime(12)
				if (rest.Length < 13) continue;
				long ticks = Num(rest[11]) + Num(rest[12]);

				string user = "?";
				foreach (var line in File.ReadLines(Path.Join(dir, "status")))
				{
					if (!line.StartsWith("Uid:")) continue;
					var uid = line[4..].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
					user = users.TryGetValue(uid, out var n) ? n : uid;
					break;
				}
				rows.Add(new ProcessInfo(user, pid, name, ticks / ClockTicksPerSecond));
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				// The process ended while it was read.
			}
		}
		return rows.OrderBy(r => r.Pid).ToList();
	}

	private static Dictionary<string, string> ReadUsers(string passwdFile)
	{
		var users = new Dictionary<string, string>();
		if (!File.Exists(passwdFile)) return users;
		foreach (var line in File.ReadLines(passwdFile))
		{
			var p = line.Split(':');
			if (p.Length > 2 && !users.ContainsKey(p[2])) users[p[2]] = p[0];
		}
		return users;
	}

	// ---- listening sockets: /proc/net/{tcp,tcp6,udp,udp6}; the owner through /proc/<pid>/fd (all of them only as root)

	public static List<ListeningPort> ReadListeningPorts(string procRoot = "/proc")
	{
		var owners = ReadSocketOwners(procRoot);
		var rows = new List<ListeningPort>();
		foreach (var (file, protocol, state) in new[] { ("tcp", "tcp", "0A"), ("tcp6", "tcp", "0A"), ("udp", "udp", "07"), ("udp6", "udp", "07") })
		{
			string path = Path.Join(procRoot, "net", file);
			if (!File.Exists(path)) continue;
			bool v6 = file.EndsWith('6');
			foreach (var line in File.ReadLines(path).Skip(1))
			{
				// sl local_address rem_address st tx_queue:rx_queue tr:tm->when retrnsmt uid timeout inode ...
				var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (p.Length < 10 || p[3] != state) continue;
				var local = ParseAddress(p[1], v6);
				if (local == null) continue;
				owners.TryGetValue(p[9], out var owner);
				rows.Add(new ListeningPort(protocol, local.Value.Address, local.Value.Port, owner.Name, owner.Name == null ? null : owner.Pid));
			}
		}
		return rows.OrderBy(r => r.Protocol).ThenBy(r => r.Port).ThenBy(r => r.Address, StringComparer.Ordinal).ToList();
	}

	/// <summary>"0100007F:0035" (IPv4, each 32 bit word printed as little endian hex) or the 32 hex digits of IPv6, then ":" and the port in hex.</summary>
	public static (string Address, int Port)? ParseAddress(string text, bool v6)
	{
		int colon = text.IndexOf(':');
		if (colon < 1) return null;
		string hex = text[..colon];
		if (!int.TryParse(text[(colon + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int port)) return null;

		byte[] bytes;
		try { bytes = Convert.FromHexString(hex); }
		catch (FormatException) { return null; }
		if (v6 ? bytes.Length != 16 : bytes.Length != 4) return null;
		// Every 4 bytes are one little endian word.
		for (int i = 0; i + 3 < bytes.Length; i += 4) Array.Reverse(bytes, i, 4);
		var ip = new IPAddress(bytes);
		// ::ffff:1.2.3.4 is an IPv4 address on a dual stack socket: show it as such.
		if (v6 && ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
		return (ip.ToString(), port);
	}

	private static Dictionary<string, (string Name, int Pid)> ReadSocketOwners(string procRoot)
	{
		var owners = new Dictionary<string, (string, int)>();
		foreach (var dir in Directory.EnumerateDirectories(procRoot))
		{
			if (!int.TryParse(Path.GetFileName(dir), out int pid)) continue;
			try
			{
				string name = File.ReadAllText(Path.Join(dir, "comm")).Trim();
				foreach (var fd in Directory.EnumerateFileSystemEntries(Path.Join(dir, "fd")))
				{
					string? target = new FileInfo(fd).LinkTarget;
					if (target == null || !target.StartsWith("socket:[")) continue;
					owners.TryAdd(target[8..^1], (name, pid));
				}
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				// Another user's process, or one that ended: no owner for its sockets.
			}
		}
		return owners;
	}

	// ---- disks: /proc/mounts for what is mounted, statfs (DriveInfo) for how full

	private static readonly HashSet<string> NotDisks = new() { "tmpfs", "devtmpfs", "proc", "sysfs", "cgroup", "cgroup2", "devpts", "mqueue", "securityfs",
		"debugfs", "tracefs", "pstore", "bpf", "fusectl", "configfs", "autofs", "hugetlbfs", "binfmt_misc", "ramfs", "rpc_pipefs", "nsfs", "efivarfs", "fuse.lxcfs",
		// read-only images (snap packages mount as squashfs and always read 100 percent full): not a disk anyone fills
		"squashfs", "erofs" };

	private static long Mb(long bytes) => (bytes + 1048575) / 1048576;

	/// <summary>/proc/mounts writes a space, a tab, a newline and a backslash in a name as \040, \011, \012 and \134.</summary>
	private static string Unescape(string s) =>
		System.Text.RegularExpressions.Regex.Replace(s, @"\\([0-7]{3})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 8)).ToString());

	public static List<DiskInfo> ReadDisks(string procRoot = "/proc")
	{
		string file = Path.Join(procRoot, "mounts");
		var rows = new List<DiskInfo>();
		if (!File.Exists(file)) return rows;
		// df shows a file system once, at its first mount point; a bind mount of the same source is not another disk.
		var seen = new HashSet<string>();
		foreach (var line in File.ReadLines(file))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (p.Length < 3 || NotDisks.Contains(p[2])) continue;
			string source = Unescape(p[0]), mount = Unescape(p[1]);
			if (!seen.Add(source)) continue;
			try
			{
				var drive = new DriveInfo(mount);
				long total = drive.TotalSize;
				if (total <= 0) continue;
				long usedBytes = total - drive.TotalFreeSpace;
				long availableBytes = drive.AvailableFreeSpace;
				// df: percent of what a normal user can use, rounded up; sizes in MB rounded up too (df -m).
				long usable = usedBytes + availableBytes;
				int percent = usable <= 0 ? 0 : (int)((usedBytes * 100 + usable - 1) / usable);
				rows.Add(new DiskInfo(source, mount, Mb(total), Mb(usedBytes), Mb(availableBytes), percent));
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
			{
				// Not readable for this user.
			}
		}
		return rows;
	}
}
