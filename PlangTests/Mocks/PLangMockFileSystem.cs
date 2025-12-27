using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using System.IO.Abstractions.TestingHelpers;

namespace PLangTests.Mocks
{
    public class PLangMockFileSystem : MockFileSystem, IPLangFileSystem
	{
		public PLangMockFileSystem() : base() { 
			Id = Guid.NewGuid().ToString();
		}
		public string RootDirectory => Environment.CurrentDirectory;

		public bool IsRootApp => true;

		public string RelativeAppPath => Path.DirectorySeparatorChar.ToString();

		public string SharedPath => Path.DirectorySeparatorChar.ToString();
		public string GoalsPath => RootDirectory;
		public string BuildPath => Path.Join(RootDirectory, ".build");
		public string DbPath => Path.Join(RootDirectory, ".db");

		public string SystemDirectory => "";

		public string Id { get;init; }

		public string OsDirectory => throw new NotImplementedException();

		public string? ValidatePath(string? path)
		{
			return path;
		}

		public void AddStep(GoalStep step)
		{
			AddFile(step.AbsolutePrFilePath, JsonConvert.SerializeObject(step));
		}

		public void AddInstruction(string path, Instruction instruction)
		{
			AddFile(path, JsonConvert.SerializeObject(instruction));
		}

		public void Init(ISettings settings, ILogger logger, ILlmService llmService)
		{
			
		}

		public void SetFileAccess(List<FileAccessControl> fileAccesses)
		{
			
		}

		public bool IsPlangRooted(string? path)
		{
			throw new NotImplementedException();
		}

		public void AddFileAccess(FileAccessControl fileAccess)
		{
			throw new NotImplementedException();
		}

		public void SetRootDirectory(string path)
		{
			throw new NotImplementedException();
		}

		public void ClearFileAccess()
		{
			throw new NotImplementedException();
		}

		public bool IsOsRooted(string path)
		{
			throw new NotImplementedException();
		}

		public List<FileAccessControl> GetFileAccesses()
		{
			throw new NotImplementedException();
		}

		public void SetRoot(string path)
		{
			throw new NotImplementedException();
		}
	}
}
