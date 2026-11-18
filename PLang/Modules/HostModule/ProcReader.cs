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

	// ---- tcp: /proc/net/tcp and tcp6 for the connections, /proc/net/snmp and netstat for the kernel's counters

	private static readonly Dictionary<string, string> TcpStates = new()
	{
		["01"] = "established", ["02"] = "syn_sent", ["03"] = "syn_recv", ["04"] = "fin_wait1", ["05"] = "fin_wait2", ["06"] = "time_wait",
		["07"] = "close", ["08"] = "close_wait", ["09"] = "last_ack", ["0A"] = "listen", ["0B"] = "closing", ["0C"] = "new_syn_recv"
	};

	private static long Hex(string s) => long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;

	/// <summary>Every TCP connection (and listening socket) of the machine, one row each. A busy server has thousands: for the numbers alone use <see cref="ReadTcpStats"/>.</summary>
	public static List<TcpConnection> ReadTcpConnections(string procRoot = "/proc")
	{
		var rows = new List<TcpConnection>();
		foreach (var file in new[] { "tcp", "tcp6" })
		{
			string path = Path.Join(procRoot, "net", file);
			if (!File.Exists(path)) continue;
			bool v6 = file.EndsWith('6');
			foreach (var line in File.ReadLines(path).Skip(1))
			{
				// sl local_address rem_address st tx_queue:rx_queue tr:tm->when retrnsmt uid timeout inode ...
				var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (p.Length < 8) continue;
				var local = ParseAddress(p[1], v6);
				var remote = ParseAddress(p[2], v6);
				if (local == null || remote == null) continue;
				var queues = p[4].Split(':');
				rows.Add(new TcpConnection("tcp", TcpStates.GetValueOrDefault(p[3].ToUpperInvariant(), p[3]), local.Value.Address, local.Value.Port,
					remote.Value.Address, remote.Value.Port, queues.Length > 1 ? Hex(queues[1]) : 0, Hex(queues[0]), (int)Num(p[7])));
			}
		}
		return rows;
	}

	public static TcpStats ReadTcpStats(string procRoot = "/proc")
	{
		int established = 0, synRecv = 0, timeWait = 0, closeWait = 0, total = 0;
		long listenQueueMax = 0;
		// The files are read line by line and never kept: only the counts are, so a server with many connections costs time but no memory.
		foreach (var file in new[] { "tcp", "tcp6" })
		{
			string path = Path.Join(procRoot, "net", file);
			if (!File.Exists(path)) continue;
			foreach (var line in File.ReadLines(path).Skip(1))
			{
				var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (p.Length < 8) continue;
				total++;
				switch (p[3].ToUpperInvariant())
				{
					case "01": established++; break;
					case "03": synRecv++; break;
					case "06": timeWait++; break;
					case "08": closeWait++; break;
					case "0A":
						var queues = p[4].Split(':');
						if (queues.Length > 1) listenQueueMax = Math.Max(listenQueueMax, Hex(queues[1]));
						break;
				}
			}
		}
		var tcp = ReadCounterTable(Path.Join(procRoot, "net", "snmp"), "Tcp:");
		var ext = ReadCounterTable(Path.Join(procRoot, "net", "netstat"), "TcpExt:");
		return new TcpStats(established, synRecv, timeWait, closeWait, total, listenQueueMax,
			tcp.GetValueOrDefault("RetransSegs"), tcp.GetValueOrDefault("AttemptFails"), tcp.GetValueOrDefault("EstabResets"),
			ext.GetValueOrDefault("ListenOverflows"), ext.GetValueOrDefault("ListenDrops"), ext.GetValueOrDefault("TCPTimeouts"),
			tcp.GetValueOrDefault("InSegs"), tcp.GetValueOrDefault("OutSegs"), tcp.GetValueOrDefault("InErrs"));
	}

	/// <summary>snmp and netstat: a line "Tcp: Name1 Name2 ..." followed by "Tcp: 1 2 ...". Returns Name to value.</summary>
	private static Dictionary<string, long> ReadCounterTable(string path, string prefix)
	{
		var values = new Dictionary<string, long>();
		if (!File.Exists(path)) return values;
		string[]? names = null;
		foreach (var line in File.ReadLines(path))
		{
			if (!line.StartsWith(prefix)) continue;
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (names == null) { names = p; continue; }
			for (int i = 1; i < p.Length && i < names.Length; i++) values[names[i]] = Num(p[i]);
			break;
		}
		return values;
	}

	// ---- pressure: /proc/pressure/{cpu,memory,io}, the share of time tasks waited for the resource (PSI)

	public static List<PressureRow> ReadPressure(string procRoot = "/proc")
	{
		var rows = new List<PressureRow>();
		foreach (var resource in new[] { "cpu", "memory", "io" })
		{
			string path = Path.Join(procRoot, "pressure", resource);
			if (!File.Exists(path)) continue;
			try
			{
				foreach (var line in File.ReadLines(path))
				{
					// "some avg10=0.00 avg60=0.00 avg300=0.00 total=12345"; cpu has no "full" line on older kernels, and then no row.
					var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					if (p.Length < 5 || (p[0] != "some" && p[0] != "full")) continue;
					var values = new Dictionary<string, string>();
					foreach (var pair in p.Skip(1))
					{
						int eq = pair.IndexOf('=');
						if (eq > 0) values[pair[..eq]] = pair[(eq + 1)..];
					}
					if (!values.ContainsKey("avg10") || !values.ContainsKey("avg60") || !values.ContainsKey("avg300") || !values.ContainsKey("total")) continue;
					rows.Add(new PressureRow(resource, p[0], Dec(values["avg10"]), Dec(values["avg60"]), Dec(values["avg300"]), Num(values["total"])));
				}
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				// Not readable: no rows for this resource.
			}
		}
		return rows;
	}

	private static double Dec(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

	// ---- cpu: the first line of /proc/stat, ticks since boot for all CPUs together

	public static CpuTicks? ReadCpuTicks(string procRoot = "/proc")
	{
		string file = Path.Join(procRoot, "stat");
		if (!File.Exists(file)) return null;
		try
		{
			foreach (var line in File.ReadLines(file))
			{
				if (!line.StartsWith("cpu ")) continue;
				// cpu user nice system idle iowait irq softirq steal guest guest_nice; a field an older kernel does not write counts as 0.
				var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				long Field(int i) => i < p.Length ? Num(p[i]) : 0;
				long user = Field(1) + Field(2);
				long system = Field(3) + Field(6) + Field(7);
				long idle = Field(4), iowait = Field(5), steal = Field(8);
				// guest and guest_nice are already counted in user and nice: they are not added again.
				return new CpuTicks(user + system + idle + iowait + steal, user, system, idle, iowait, steal);
			}
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			// Not readable.
		}
		return null;
	}

	// ---- one process: /proc/<pid>/stat (name, cpu time, start), /proc/<pid>/status (memory, threads), /proc/<pid>/fd (open files)

	public static ProcessStats? ReadProcessStats(int pid, string procRoot = "/proc")
	{
		if (pid <= 0) return null;
		string dir = Path.Join(procRoot, pid.ToString(CultureInfo.InvariantCulture));
		if (!Directory.Exists(dir)) return null;
		try
		{
			string stat = File.ReadAllText(Path.Join(dir, "stat"));
			// "pid (name) state ...": the name may hold spaces and brackets, so cut at the LAST ")".
			int open = stat.IndexOf('('), close = stat.LastIndexOf(')');
			if (open < 0 || close < open) return null;
			string name = stat[(open + 1)..close];
			var rest = stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
			// after the name: state(0) ppid(1) ... utime(11) stime(12) ... starttime(19)
			if (rest.Length < 20) return null;
			long cpuSeconds = (Num(rest[11]) + Num(rest[12])) / ClockTicksPerSecond;
			long startTicks = Num(rest[19]);

			long rssKb = 0;
			int threads = 0;
			foreach (var line in File.ReadLines(Path.Join(dir, "status")))
			{
				// "VmRSS:	  123456 kB"; a kernel thread has no VmRSS line and uses no memory of its own.
				if (line.StartsWith("VmRSS:")) rssKb = Num(line[6..].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");
				else if (line.StartsWith("Threads:")) threads = (int)Num(line[8..].Trim());
			}

			return new ProcessStats(pid, name, rssKb / 1024, threads, CountFds(dir), cpuSeconds, ReadAgeSeconds(procRoot, startTicks));
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			// The process ended while it was read.
			return null;
		}
	}

	/// <summary>Open file handles (sockets and pipes included) of a process: the entries of /proc/&lt;pid&gt;/fd, null when they cannot be read.</summary>
	public static int? CountOpenFiles(int pid, string procRoot = "/proc")
	{
		return CountFds(Path.Join(procRoot, pid.ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>
	/// The "kB" lines of /proc/&lt;pid&gt;/status (VmRSS, RssAnon, RssFile, RssShmem, VmSwap, ...) by name, in kB.
	/// RssAnon is memory the process allocated itself (managed heap, native allocations, thread stacks), RssFile is mapped files
	/// such as the loaded dlls. Empty when the file cannot be read.
	/// </summary>
	public static Dictionary<string, long> ReadStatusKb(int pid, string procRoot = "/proc")
	{
		var result = new Dictionary<string, long>(StringComparer.Ordinal);
		try
		{
			string file = Path.Join(procRoot, pid.ToString(CultureInfo.InvariantCulture), "status");
			if (!File.Exists(file)) return result;
			foreach (var line in File.ReadLines(file))
			{
				int colon = line.IndexOf(':');
				if (colon <= 0 || !line.EndsWith(" kB")) continue;
				var number = line[(colon + 1)..^3].Trim();
				if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long kb)) result[line[..colon]] = kb;
			}
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			// Not readable.
		}
		return result;
	}

	/// <summary>Entries of /proc/&lt;pid&gt;/fd; another user's process can only be counted by root, and then the count is null.</summary>
	private static int? CountFds(string processDir)
	{
		try
		{
			return Directory.EnumerateFileSystemEntries(Path.Join(processDir, "fd")).Count();
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			return null;
		}
	}

	/// <summary>Seconds since the process started: the machine's uptime (first number of /proc/uptime) minus the process's start, in ticks after boot.</summary>
	private static long? ReadAgeSeconds(string procRoot, long startTicks)
	{
		try
		{
			string file = Path.Join(procRoot, "uptime");
			if (!File.Exists(file)) return null;
			var first = File.ReadAllText(file).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
			if (first == null || !double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out double uptime)) return null;
			return Math.Max(0, (long)(uptime - (double)startTicks / ClockTicksPerSecond));
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			return null;
		}
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
