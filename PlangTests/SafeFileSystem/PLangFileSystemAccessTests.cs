using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Exceptions;
using PLang.SafeFileSystem;

namespace PLangTests.SafeFileSystem
{
	[TestClass]
	public class PLangFileSystemAccessTests
	{
		private string appPath = null!;
		private PLangFileSystem fileSystem = null!;

		[TestInitialize]
		public void Init()
		{
			if (OperatingSystem.IsWindows()) Assert.Inconclusive("OS paths in these tests are Linux paths");

			appPath = System.IO.Path.Join(System.IO.Path.GetTempPath(),"plang-fileaccess-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(appPath);
			fileSystem = new PLangFileSystem(appPath, "/");
		}

		[TestCleanup]
		public void Cleanup()
		{
			if (appPath != null && Directory.Exists(appPath)) Directory.Delete(appPath, true);
		}

		private void GiveAccess(PLangFileSystem fs, string path)
		{
			fs.AddFileAccess(new FileAccessControl(fs.RootDirectory, path, ProcessId: fs.Id));
		}

		[TestMethod]
		public void ValidatePath_OsPathWithoutGrant_ThrowsFileAccessException()
		{
			var ex = Assert.ThrowsException<FileAccessException>(() => fileSystem.ValidatePath("//proc/uptime"));
			Assert.AreEqual("/proc/uptime", ex.Path);
		}

		[TestMethod]
		public void ValidatePath_OnlyGrantIsProc_AllowsOsPathUnderProc()
		{
			GiveAccess(fileSystem, "/proc");

			Assert.AreEqual("/proc/uptime", fileSystem.ValidatePath("//proc/uptime"));
		}

		[TestMethod]
		public void SetFileAccess_KeepsEveryProcessGrant()
		{
			GiveAccess(fileSystem, "/etc");
			GiveAccess(fileSystem, "/proc");

			// what FileAccessHandler does after an answered access question: the stored grants replace the list
			var stored = new List<FileAccessControl> { new(fileSystem.RootDirectory, "/var/log", DateTime.UtcNow.AddYears(1)) };
			fileSystem.SetFileAccess(stored);

			Assert.AreEqual("/etc/hostname", fileSystem.ValidatePath("//etc/hostname"));
			Assert.AreEqual("/proc/uptime", fileSystem.ValidatePath("//proc/uptime"));
			Assert.AreEqual("/var/log/syslog", fileSystem.ValidatePath("//var/log/syslog"));
		}

		[TestMethod]
		public void AddFileAccess_SameGrantTwice_IsKeptOnce()
		{
			GiveAccess(fileSystem, "/proc");
			int count = fileSystem.GetProcessFileAccess().Count;

			GiveAccess(fileSystem, "/proc");

			Assert.AreEqual(count, fileSystem.GetProcessFileAccess().Count);
		}

		[TestMethod]
		public void GetProcessFileAccess_CopiedToAnotherFileSystem_GrantsAccessThereUntilCleared()
		{
			// a rented engine has its own file system; Engine.RentAsync copies the caller's process grants to it
			GiveAccess(fileSystem, "/proc");
			var rented = new PLangFileSystem(appPath, "/");
			Assert.ThrowsException<FileAccessException>(() => rented.ValidatePath("//proc/uptime"));

			foreach (var fileAccess in fileSystem.GetProcessFileAccess())
			{
				rented.AddFileAccess(fileAccess with { ProcessId = rented.Id });
			}
			Assert.AreEqual("/proc/uptime", rented.ValidatePath("//proc/uptime"));

			rented.ClearFileAccess();
			Assert.ThrowsException<FileAccessException>(() => rented.ValidatePath("//proc/uptime"));
		}
	}
}
