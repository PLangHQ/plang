using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.HostModule;

namespace PLangTests.Modules.HostModule
{
	[TestClass]
	public class ProcReaderTests
	{
		private string procRoot = null!;

		[TestInitialize]
		public void Init()
		{
			procRoot = Path.Join(Path.GetTempPath(), "plang-proc-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(procRoot);
		}

		[TestCleanup]
		public void Cleanup()
		{
			foreach (var dir in Directory.EnumerateDirectories(procRoot, "*", SearchOption.AllDirectories))
			{
				if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			}
			Directory.Delete(procRoot, true);
		}

		private void Write(string relativePath, string content)
		{
			string path = Path.Join(procRoot, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, content);
		}

		// ---- pressure

		[TestMethod]
		public void ReadPressure_AllLines_OneRowEach()
		{
			Write("pressure/cpu", "some avg10=7.37 avg60=8.47 avg300=7.68 total=3986664385\nfull avg10=0.00 avg60=0.00 avg300=0.00 total=0\n");
			Write("pressure/memory", "some avg10=0.10 avg60=0.20 avg300=0.30 total=100\nfull avg10=0.01 avg60=0.02 avg300=0.03 total=50\n");
			Write("pressure/io", "some avg10=1.50 avg60=2.25 avg300=3.00 total=999\nfull avg10=1.00 avg60=2.00 avg300=2.50 total=888\n");

			var rows = ProcReader.ReadPressure(procRoot);

			Assert.AreEqual(6, rows.Count);
			var cpuSome = rows.Single(r => r.Resource == "cpu" && r.Kind == "some");
			Assert.AreEqual(7.37, cpuSome.Avg10, 1e-9);
			Assert.AreEqual(8.47, cpuSome.Avg60, 1e-9);
			Assert.AreEqual(7.68, cpuSome.Avg300, 1e-9);
			Assert.AreEqual(3986664385L, cpuSome.Total);
			var ioFull = rows.Single(r => r.Resource == "io" && r.Kind == "full");
			Assert.AreEqual(2.5, ioFull.Avg300, 1e-9);
			Assert.AreEqual(888L, ioFull.Total);
		}

		[TestMethod]
		public void ReadPressure_CpuWithoutFullLine_NoCpuFullRow()
		{
			Write("pressure/cpu", "some avg10=1.00 avg60=2.00 avg300=3.00 total=10\n");
			Write("pressure/memory", "some avg10=0.00 avg60=0.00 avg300=0.00 total=0\nfull avg10=0.00 avg60=0.00 avg300=0.00 total=0\n");
			Write("pressure/io", "some avg10=0.00 avg60=0.00 avg300=0.00 total=0\nfull avg10=0.00 avg60=0.00 avg300=0.00 total=0\n");

			var rows = ProcReader.ReadPressure(procRoot);

			Assert.AreEqual(5, rows.Count);
			Assert.IsFalse(rows.Any(r => r.Resource == "cpu" && r.Kind == "full"));
			Assert.IsTrue(rows.Any(r => r.Resource == "cpu" && r.Kind == "some"));
		}

		[TestMethod]
		public void ReadPressure_NoPressureDir_EmptyList()
		{
			var rows = ProcReader.ReadPressure(procRoot);

			Assert.IsNotNull(rows);
			Assert.AreEqual(0, rows.Count);
		}

		// ---- cpu

		[TestMethod]
		public void ReadCpuTicks_FirstCpuLine_SumsWithoutGuest()
		{
			// user nice system idle iowait irq softirq steal guest guest_nice
			Write("stat", "cpu  1000 200 300 5000 40 5 6 7 900 90\ncpu0 1 1 1 1 1 1 1 1 1 1\nintr 123\n");

			var ticks = ProcReader.ReadCpuTicks(procRoot);

			Assert.IsNotNull(ticks);
			Assert.AreEqual(1200L, ticks.User);
			Assert.AreEqual(311L, ticks.System);
			Assert.AreEqual(5000L, ticks.Idle);
			Assert.AreEqual(40L, ticks.Iowait);
			Assert.AreEqual(7L, ticks.Steal);
			Assert.AreEqual(1000L + 200 + 300 + 5000 + 40 + 5 + 6 + 7, ticks.Total);
			Assert.AreEqual(ticks.User + ticks.System + ticks.Idle + ticks.Iowait + ticks.Steal, ticks.Total);
		}

		[TestMethod]
		public void ReadCpuTicks_NoFile_Null()
		{
			Assert.IsNull(ProcReader.ReadCpuTicks(procRoot));
		}

		// ---- one process

		private void WriteProcess(int pid, string name, long utime, long stime, long starttime, string status, int fds)
		{
			// pid (name) state ppid pgrp session tty_nr tpgid flags minflt cminflt majflt cmajflt utime stime cutime cstime priority nice num_threads itrealvalue starttime vsize rss
			Write($"{pid}/stat", $"{pid} ({name}) S 1 {pid} {pid} 0 -1 4194560 100 0 0 0 {utime} {stime} 0 0 20 0 3 0 {starttime} 123456 789\n");
			Write($"{pid}/status", status);
			Directory.CreateDirectory(Path.Join(procRoot, pid.ToString(), "fd"));
			for (int i = 0; i < fds; i++) File.WriteAllText(Path.Join(procRoot, pid.ToString(), "fd", i.ToString()), "");
		}

		private const string Status = "Name:\tmy (odd) name\nState:\tS (sleeping)\nVmRSS:\t  204800 kB\nThreads:\t17\n";

		[TestMethod]
		public void ReadProcessStats_NameWithSpacesAndBrackets_AllFields()
		{
			WriteProcess(1234, "my (odd) name", 4500, 1550, 50000, Status, 5);
			Write("uptime", "1000.50 4000.00\n");

			var p = ProcReader.ReadProcessStats(1234, procRoot);

			Assert.IsNotNull(p);
			Assert.AreEqual(1234, p.Pid);
			Assert.AreEqual("my (odd) name", p.Name);
			Assert.AreEqual(200L, p.RssMb);
			Assert.AreEqual(17, p.Threads);
			Assert.AreEqual(5, p.Fds);
			Assert.AreEqual(60L, p.CpuSeconds);
			// uptime 1000.5 s - starttime 50000 ticks / 100 = 500.5 s
			Assert.AreEqual(500L, p.AgeSeconds);
		}

		[TestMethod]
		public void ReadProcessStats_MissingPid_Null()
		{
			Write("uptime", "1000.50 4000.00\n");

			Assert.IsNull(ProcReader.ReadProcessStats(4321, procRoot));
		}

		[TestMethod]
		public void ReadProcessStats_NoFdDir_FdsNull()
		{
			WriteProcess(77, "worker", 0, 0, 0, Status, 0);
			Directory.Delete(Path.Join(procRoot, "77", "fd"));
			Write("uptime", "10.00 10.00\n");

			var p = ProcReader.ReadProcessStats(77, procRoot);

			Assert.IsNotNull(p);
			Assert.IsNull(p.Fds);
			Assert.AreEqual(10L, p.AgeSeconds);
		}

		[TestMethod]
		public void ReadProcessStats_FdDirNotReadable_FdsNull()
		{
			if (OperatingSystem.IsWindows()) Assert.Inconclusive("Unix file modes only.");
			WriteProcess(88, "worker", 0, 0, 0, Status, 3);
			string fd = Path.Join(procRoot, "88", "fd");
			File.SetUnixFileMode(fd, UnixFileMode.None);
			if (Environment.UserName == "root") Assert.Inconclusive("root reads a directory whatever its mode.");

			var p = ProcReader.ReadProcessStats(88, procRoot);

			Assert.IsNotNull(p);
			Assert.IsNull(p.Fds);
		}

		// ---- tcp

		private const string TcpHeader = "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode\n";

		private void WriteTcp()
		{
			Write("net/tcp", TcpHeader +
				"   0: 00000000:0050 00000000:0000 0A 00000000:00000003 00:00000000 00000000     0        0 1001 1 0000000000000000 100 0 0 10 0\n" +
				"   1: 0100007F:0050 0100007F:C350 01 00000010:00000020 00:00000000 00000000    33        0 1002 1 0000000000000000 20 4 30 10 -1\n" +
				"   2: 0100007F:0050 0100007F:C351 06 00000000:00000000 00:00000000 00000000     0        0 0 1 0000000000000000 20 4 30 10 -1\n");
			Write("net/tcp6", TcpHeader +
				"   0: 0000000000000000FFFF00000100007F:1F90 0000000000000000FFFF00000200007F:D431 08 00000000:00000000 00:00000000 00000000  1000        0 1003 1 0000000000000000 20 4 30 10 -1\n");
		}

		[TestMethod]
		public void ReadTcpStats_CountsByStateAndCounters()
		{
			WriteTcp();
			Write("net/snmp", "Ip: Forwarding DefaultTTL\nIp: 1 64\nTcp: RtoAlgorithm AttemptFails EstabResets RetransSegs\nTcp: 1 4 5 6\n");
			Write("net/netstat", "TcpExt: ListenOverflows ListenDrops TCPTimeouts\nTcpExt: 7 8 9\n");

			var s = ProcReader.ReadTcpStats(procRoot);

			Assert.AreEqual(1, s.Established);
			Assert.AreEqual(1, s.TimeWait);
			Assert.AreEqual(1, s.CloseWait);
			Assert.AreEqual(0, s.SynRecv);
			Assert.AreEqual(4, s.Total);
			Assert.AreEqual(3L, s.ListenQueueMax);
			Assert.AreEqual(6L, s.RetransSegs);
			Assert.AreEqual(4L, s.AttemptFails);
			Assert.AreEqual(5L, s.EstabResets);
			Assert.AreEqual(7L, s.ListenOverflows);
			Assert.AreEqual(8L, s.ListenDrops);
			Assert.AreEqual(9L, s.Timeouts);
		}

		[TestMethod]
		public void ReadTcpConnections_OneRowPerLine()
		{
			WriteTcp();

			var rows = ProcReader.ReadTcpConnections(procRoot);

			Assert.AreEqual(4, rows.Count);
			var established = rows.Single(r => r.State == "established");
			Assert.AreEqual("127.0.0.1", established.LocalAddress);
			Assert.AreEqual(80, established.LocalPort);
			Assert.AreEqual(50000, established.RemotePort);
			Assert.AreEqual(0x10L, established.TxQueue);
			Assert.AreEqual(0x20L, established.RxQueue);
			Assert.AreEqual(33, established.Uid);
			var v6 = rows.Single(r => r.State == "close_wait");
			Assert.AreEqual("127.0.0.1", v6.LocalAddress);
			Assert.AreEqual(8080, v6.LocalPort);
			Assert.AreEqual("127.0.0.2", v6.RemoteAddress);
			Assert.AreEqual(1000, v6.Uid);
		}
	}
}
