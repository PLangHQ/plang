using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using static PLang.SafeFileSystem.FileAccessHandler;

namespace PLangTests.SafeFileSystem
{
	[TestClass]
	public class FileAccessHandlerTests
	{
		private string appPath = null!;
		private PLangFileSystem fileSystem = null!;
		private ISettings settings = null!;
		private ILlmService llmService = null!;
		private FileAccessHandler handler = null!;

		[TestInitialize]
		public void Init()
		{
			if (OperatingSystem.IsWindows()) Assert.Inconclusive("OS paths in these tests are Linux paths");

			appPath = System.IO.Path.Join(System.IO.Path.GetTempPath(), "plang-fileaccesshandler-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(appPath);
			fileSystem = new PLangFileSystem(appPath, "/");

			settings = Substitute.For<ISettings>();
			settings.GetValues<FileAccessControl>(Arg.Any<Type>()).Returns(_ => new List<FileAccessControl>());

			llmService = Substitute.For<ILlmService>();
			var llmServiceFactory = Substitute.For<ILlmServiceFactory>();
			llmServiceFactory.CreateHandler().Returns(llmService);

			handler = new FileAccessHandler(settings, llmServiceFactory, Substitute.For<ILogger>(), fileSystem);
		}

		[TestCleanup]
		public void Cleanup()
		{
			if (appPath != null && Directory.Exists(appPath)) Directory.Delete(appPath, true);
		}

		[DataTestMethod]
		[DataRow("no")]
		[DataRow("n")]
		[DataRow("No")]
		[DataRow(" NO \n")]
		[DataRow("no thanks")]
		[DataRow("Nope.")]
		[DataRow("deny")]
		public async Task ValidatePathResponse_NegativeAnswer_ReturnsAccessDeniedAndGrantsNothing(string answer)
		{
			var (isHandled, error) = await handler.ValidatePathResponse(fileSystem.RootDirectory, "/proc/uptime", answer, fileSystem.Id);

			Assert.IsFalse(isHandled);
			Assert.IsNotNull(error);
			Assert.AreEqual(FileAccessDeniedKey, error.Key);
			Assert.AreEqual(403, error.StatusCode);
			StringAssert.Contains(error.Message, "refused");
			Assert.ThrowsException<FileAccessException>(() => fileSystem.ValidatePath("//proc/uptime"));
			await llmService.DidNotReceiveWithAnyArgs().Query<FileAccessResponse>(default!);
		}

		[TestMethod]
		public async Task ValidatePathResponse_LlmMapsAnswerToNo_ReturnsAccessDenied()
		{
			llmService.Query<FileAccessResponse>(Arg.Any<LlmRequest>()).Returns((new FileAccessResponse("no", null), (IError?)null));

			var (isHandled, error) = await handler.ValidatePathResponse(fileSystem.RootDirectory, "/proc/uptime", "I'd rather you didn't", fileSystem.Id);

			Assert.IsFalse(isHandled);
			Assert.AreEqual(FileAccessDeniedKey, error?.Key);
			Assert.ThrowsException<FileAccessException>(() => fileSystem.ValidatePath("//proc/uptime"));
		}

		[TestMethod]
		public async Task ValidatePathResponse_EmptyAnswer_IsNotHandledAndNotDenied()
		{
			var (isHandled, error) = await handler.ValidatePathResponse(fileSystem.RootDirectory, "/proc/uptime", "", fileSystem.Id);

			Assert.IsFalse(isHandled);
			Assert.IsNull(error);
		}

		[DataTestMethod]
		[DataRow("yes")]
		[DataRow("y")]
		[DataRow("always")]
		[DataRow("a")]
		public async Task ValidatePathResponse_PositiveAnswer_GrantsAccess(string answer)
		{
			var (isHandled, error) = await handler.ValidatePathResponse(fileSystem.RootDirectory, "/proc", answer, fileSystem.Id);

			Assert.IsTrue(isHandled);
			Assert.IsNull(error);
			Assert.AreEqual("/proc/uptime", fileSystem.ValidatePath("//proc/uptime"));
		}

		[TestMethod]
		public async Task ValidatePathResponse_LlmMapsAnswerToYesWithExpiry_GrantsAccess()
		{
			var expires = DateTime.UtcNow.AddDays(30);
			llmService.Query<FileAccessResponse>(Arg.Any<LlmRequest>()).Returns((new FileAccessResponse("yes", expires), (IError?)null));

			var (isHandled, error) = await handler.ValidatePathResponse(fileSystem.RootDirectory, "/proc", "yes for 30 days", fileSystem.Id);

			Assert.IsTrue(isHandled);
			Assert.IsNull(error);
			Assert.AreEqual("/proc/uptime", fileSystem.ValidatePath("//proc/uptime"));
		}

		[DataTestMethod]
		[DataRow("/proc")]
		[DataRow("//proc")]
		[DataRow("///proc")]
		[DataRow("////proc")]
		[DataRow("///proc/")]
		public void GiveAccess_OsPathWithAnyNumberOfLeadingSlashes_MatchesValidatedPath(string grant)
		{
			handler.GiveAccess(fileSystem.RootDirectory, grant);

			Assert.AreEqual("/proc/uptime", fileSystem.ValidatePath("//proc/uptime"));
			Assert.IsTrue(fileSystem.GetProcessFileAccess().Any(p => p.path == "/proc" || p.path == "/proc/"));
		}

		[TestMethod]
		public void GiveAccess_AppRelativePath_IsStoredUnchanged()
		{
			handler.GiveAccess(fileSystem.RootDirectory, "data/files");

			Assert.IsTrue(fileSystem.GetProcessFileAccess().Any(p => p.path == "data/files"));
		}
	}
}
