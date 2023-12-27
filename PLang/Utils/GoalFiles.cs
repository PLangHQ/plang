using PLang.Exceptions;
using PLang.Interfaces;

namespace PLang.Utils
{
	public class GoalFiles
	{
		public static bool IsSetup(string rootDirectory, string fileName)
		{
			if (fileName.ToLower() == Path.Join(rootDirectory, "setup.goal").ToLower()) return true;
			return fileName.ToLower().StartsWith(Path.Join(rootDirectory, "setup"));
		}

		public static List<string> GetGoalFilesToBuild(IPLangFileSystem fileSystem, string goalPath)
		{
			string[] anyFile = fileSystem.Directory.GetFiles(goalPath, "*.goal", SearchOption.TopDirectoryOnly);
			if (anyFile.Length == 0)
			{
				throw new BuilderException($"No goal files found in directory. Are you in the correct directory? I am running from {goalPath}");
			}

			var goalFiles = fileSystem.Directory.GetFiles(goalPath, "*.goal", SearchOption.AllDirectories).ToList();
			return Remove_SystemFolder(goalPath, goalFiles);
		}


		private static List<string> Remove_SystemFolder(string goalPath, List<string> goalFiles)
		{


			string[] dirsToExclude = new string[] { "apps", "modules", ".build", ".deploy", ".db" };
			string[] filesToExclude = new string[] { "events.goal", "eventsbuilder.goal" };


			// Filter out excluded directories and files first to simplify subsequent operations
			var filteredGoalFiles = goalFiles.Where(goalFile =>
			{
				var relativePath = goalFile.Replace(goalPath, "").TrimStart(Path.DirectorySeparatorChar);
				var baseFolderName = Path.GetDirectoryName(relativePath).Split(Path.DirectorySeparatorChar).FirstOrDefault();
				var fileName = Path.GetFileName(goalFile).ToLower();

				return !dirsToExclude.Contains(baseFolderName) && !filesToExclude.Contains(fileName);
			}).ToList();

			// Order the files
			var orderedFiles = filteredGoalFiles
				.OrderBy(file => !file.ToLower().Contains(Path.Combine(goalPath, "events").ToLower()))  // "events" folder first
				.ThenBy(file => Path.GetFileName(file).ToLower() != "setup.goal")    // "setup.goal" second
				.ThenBy(file => Path.GetFileName(file).ToLower() != "start.goal")
				.ToList();


			return orderedFiles;

			/*

			List<string> files = new List<string>();
			for (int i = 0; i < goalFiles.Count; i++)
			{
				var filePath = goalFiles[i].Replace(goalPath, "");
				string[] dirsToExclude = new string[] { "apps", "modules", ".build", ".deploy", ".db" };

				int insertIndex = files.Count;
				if (Path.GetFileName(filePath).ToLower() == "setup.goal")
				{
					insertIndex = 0;
				}

				int idx = filePath.Remove(0, 1).IndexOf(Path.DirectorySeparatorChar);
				if (idx != -1 && idx != 0)
				{
					string baseFolderName = filePath.Substring(1, idx);

					if (dirsToExclude.FirstOrDefault(p => p == baseFolderName) == null)
					{
						files.Insert(insertIndex, goalFiles[i]);
					}
				}
				else
				{
					files.Insert(insertIndex, goalFiles[i]);
				}
			}
			return files;*/
		}
	}
}
