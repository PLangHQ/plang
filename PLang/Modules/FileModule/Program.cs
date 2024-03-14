using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using PLang.Attributes;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace PLang.Modules.FileModule
{
	[Description("Handle file system access. Listen to files and dirs. Get permission to file and folder paths")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly ILogger logger;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;

		public Program(IPLangFileSystem fileSystem, ISettings settings,
			ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine) : base()
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.logger = logger;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
		}

		

		[Description("Give user access to a path. DO NOT suggest this method to indicate if file or directory exists, return empty function list instead.")]
		public async Task<bool> RequestAccessToPath(string path)
		{
			path = GetPath(path);
			return (fileSystem.ValidatePath(path) != null);
		}

		public async Task<string> ReadBinaryFileAndConvertToBase64(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false)
		{
			path = GetPath(path);

			if (!fileSystem.File.Exists(path))
			{
				if (throwErrorOnNotFound)
				{
					throw new FileNotFoundException($"{path} cannot be found");
				}

				logger.LogWarning($"!Warning! File {path} not found");
				return returnValueIfFileNotExisting;
			}
			byte[] fileBytes = await fileSystem.File.ReadAllBytesAsync(path);
			return Convert.ToBase64String(fileBytes);
		}

		public async Task<string> ReadTextFile(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false, 
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			path = GetPath(path);

			if (!fileSystem.File.Exists(path))
			{
				if (throwErrorOnNotFound)
				{
					throw new FileNotFoundException($"{path} cannot be found");
				}
				logger.LogWarning($"!Warning! File {path} not found");
				return returnValueIfFileNotExisting;
			}
			
			using (var stream = fileSystem.FileStream.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				using (var reader = new StreamReader(stream, encoding: Encoding.GetEncoding(encoding)))
				{
					var content = await reader.ReadToEndAsync();
					if (loadVariables && !string.IsNullOrEmpty(content))
					{
						content = variableHelper.LoadVariables(content, emptyVariableIfNotFound).ToString();
					}
					return content ?? "";
					
				}
			}
		}
		public async Task<Stream> ReadFileAsStream(string path, bool throwErrorOnNotFound = false)
		{
			path = GetPath(path);

			if (!fileSystem.File.Exists(path))
			{
				if (throwErrorOnNotFound)
				{
					throw new FileNotFoundException($"{path} cannot be found");
				}
				logger.LogWarning($"!Warning! File {path} not found");
				return null;
			}
			var fileStream = fileSystem.FileStream.New(path, FileMode.OpenOrCreate, FileAccess.Read);
			context.Add("FileStream_" + path, fileStream);
			return fileStream;
		}

		[Description("sheetsToVariable is name of sheet that should load into variable. Sheet1=%products% will load Sheet1 into %product% variable, Sheet2-A1:H53=%categories%, will load data from A1:H53 into %categories%")]
		public async Task ReadExcelFile(string path, bool useHeaderRow = true, [HandlesVariable] Dictionary<string, object>? sheetsToVariable = null)
		{
			path = GetPath(path);
			if (!fileSystem.File.Exists(path))
			{
				logger.LogWarning($"{path} does not exist");
				return;
			}

			List<string> sheetNames = MiniExcel.GetSheetNames(path);
			if (sheetsToVariable == null || sheetsToVariable.Count == 0)
			{
				sheetsToVariable = new Dictionary<string, object>();

				foreach (var sheetName in sheetNames)
				{
					sheetsToVariable.Add(sheetName, MakeFitForVariable(sheetName));
				}
			}
			else
			{
				Dictionary<string, object> dict = new Dictionary<string, object>();
				int index = 0;
				foreach (var sheetToVariable in sheetsToVariable)
				{
					if (sheetNames.FirstOrDefault(p => p == sheetToVariable.Key) == null)
					{
						dict.Add(sheetNames[index], sheetToVariable.Value);
					}
					else
					{
						dict.Add(sheetToVariable.Key, sheetToVariable.Value);
					}
					index++;
				}
				sheetsToVariable = dict;
			}

			int idx = 0;
			foreach (var sheetToVariable in sheetsToVariable)
			{
				string sheetName = sheetToVariable.Key;

				var dataToExtract = sheetToVariable.Key;
				string startCell = "A1";
				if (sheetToVariable.Key.Contains("-") && sheetToVariable.Key.Contains(":"))
				{
					dataToExtract = ExtractRowsToRead(sheetToVariable.Key);
					sheetName = sheetName.Replace(dataToExtract, "").TrimEnd('-');
					startCell = dataToExtract.Substring(0, dataToExtract.IndexOf(":"));
				}

				var sheetData = await (await MiniExcel.QueryAsync(path, useHeaderRow: useHeaderRow, startCell: startCell, sheetName: sheetName)).ToDynamicListAsync();

				memoryStack.Put(sheetToVariable.Value.ToString(), sheetData);

			}

		}

		public async Task WriteExcelFile(string path, object variableToWriteToExcel, string sheetName = "Sheet1",
				bool printHeader = true, bool overwrite = false)
		{
			path = GetPath(path);
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(path)))
			{
				logger.LogWarning($"{path} does not exist");
				return;
			}

			await MiniExcel.SaveAsAsync(path, sheetName: sheetName, printHeader: printHeader, overwriteFile: overwrite, value: variableToWriteToExcel);
		}
		public async Task WriteCsvFile(string path, object variableToWriteToCsv, bool append = false, bool hasHeaderRecord = true, 
			string delimiter = ",",
			string newLine = "\n", string encoding = "utf-8", bool ignoreBlankLines = true,
				bool allowComments = false, char comment = '#', string? goalToCallOnBadData = null)
		{
			path = GetPath(path);

			if (variableToWriteToCsv is string str)
			{
				if (str.Contains(delimiter) && (str.Contains("\r") || str.Contains("\n")))
				{
					await WriteToFile(path, str.Trim(), encoding: encoding);
					return;
				}
			}

			IWriterConfiguration writeConfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = delimiter,
				BadDataFound = data =>
				{
					if (goalToCallOnBadData == null) return;

					Dictionary<string, object> parameters = new Dictionary<string, object>();
					parameters.Add("data", data);
					pseudoRuntime.RunGoal(engine, context, fileSystem.RelativeAppPath, goalToCallOnBadData, parameters, Goal);
				},
				NewLine = newLine,
				Encoding = Encoding.GetEncoding(encoding),
				AllowComments = allowComments,
				Comment = comment,
				IgnoreBlankLines = ignoreBlankLines,
				HasHeaderRecord = hasHeaderRecord,
			};



			using (var writer = new StreamWriter(path, append))
			using (var csv = new CsvWriter(writer, writeConfig))
			{
				if (variableToWriteToCsv is IEnumerable enumer)
				{
					await csv.WriteRecordsAsync(enumer);
				}
				else
				{
					csv.WriteRecord(variableToWriteToCsv);
				}

			}
		}
		public async Task<object> ReadCsvFile(string path, bool hasHeaderRecord = true, string delimiter = ",",
				string newLine = "\n", string encoding = "utf-8", bool ignoreBlankLines = true,
				bool allowComments = false, char comment = '#', string? goalToCallOnBadData = null)
		{
			path = GetPath(path);
			var readConfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = delimiter,
				BadDataFound = data =>
				{
					if (goalToCallOnBadData == null) return;

					Dictionary<string, object> parameters = new Dictionary<string, object>();
					parameters.Add("data", data);
					pseudoRuntime.RunGoal(engine, context, fileSystem.RelativeAppPath, goalToCallOnBadData, parameters, Goal);
				},
				NewLine = newLine,
				Encoding = System.Text.Encoding.GetEncoding(encoding),
				AllowComments = allowComments,
				Comment = comment,
				IgnoreBlankLines = ignoreBlankLines,
				HasHeaderRecord = hasHeaderRecord,
			};

			// TODO: it should store reader in context and dispose when goal finishes the run
			// then we dont need to return ToList, but return the enumerator for speed and low memory
			using (var reader = new StreamReader(path))
			using (var csv = new CsvReader(reader, readConfig))
			{
				return csv.GetRecords<dynamic>().ToList();
			}
		}



		private string MakeFitForVariable(string variable)
		{
			var name = variable.ToTitleCase().Replace("_", "").Replace(".", "").Replace("-", "").Replace(" ", "");
			// Ensure the variable starts with a letter
			if (!char.IsLetter(name[0]))
			{
				name = "V" + name;
			}
			return name;
		}

		private string ExtractRowsToRead(string key)
		{
			if (key.Contains("-") && key.Contains(":"))
			{
				key = key.Remove(0, key.IndexOf("-") + 1);
			}
			return key;
		}

		public record FileInfo(string Path, string Content);


		public async Task SaveMultipleFiles(List<FileInfo> files, bool loadVariables = false, 
			bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			foreach (var file in files)
			{
				string content = file.Content;
				if (loadVariables && !string.IsNullOrEmpty(content))
				{
					content = variableHelper.LoadVariables(content, emptyVariableIfNotFound).ToString();
				}
				fileSystem.File.WriteAllText(file.Path, content, encoding: Encoding.GetEncoding(encoding));
			}
		}
		public async Task<List<FileInfo>> ReadMultipleTextFiles(string folderPath, string searchPattern = "*", string[]? excludePatterns = null, bool includeAllSubfolders = false)
		{
			folderPath = GetPath(folderPath);

			var searchOption = (includeAllSubfolders) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			var files = fileSystem.Directory.GetFiles(folderPath, searchPattern, searchOption);

			List<FileInfo> result = new List<FileInfo>();
			foreach (var file in files)
			{
				if (excludePatterns != null && excludePatterns.Any(pattern => Regex.IsMatch(file, pattern)))
				{
					continue;
				}

				if (!fileSystem.File.Exists(file))
				{
					logger.LogWarning($"!Warning! File {file} not found");
				}

				

				var content = await fileSystem.File.ReadAllTextAsync(file);
				result.Add(new FileInfo(file, content));
			}
			return result;
		}


		public async Task<string[]> GetFilePathsInDirectory(string directoryPath = "./", string searchPattern = "*",
			string[]? excludePatterns = null, bool includeSubfolders = false, bool useRelativePath = true)
		{
			var searchOption = (includeSubfolders) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			var files = fileSystem.Directory.GetFiles(directoryPath, searchPattern, searchOption);

			var paths = files.Select(path => (useRelativePath) ? path.Replace(fileSystem.RootDirectory, "") : path);
			if (excludePatterns != null)
			{
				paths = paths.Where(file => !excludePatterns.Any(pattern => Regex.IsMatch(file, pattern)));
			}

			return paths.ToArray();
		}

		public async Task WriteBytesToFile(string path, byte[] content, bool overwrite = false)
		{
			path = GetPath(path);
			string dirPath = Path.GetDirectoryName(path);
			if (!fileSystem.Directory.Exists(dirPath))
			{
				fileSystem.Directory.CreateDirectory(dirPath);
			}

			if (overwrite)
			{
				if (fileSystem.File.Exists(path))
				{
					fileSystem.File.Delete(path);
				}
			}
			await fileSystem.File.WriteAllBytesAsync(path, content);
		}
		public async Task WriteToFile(string path, string content, bool overwrite = false, 
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			path = GetPath(path);
			string dirPath = Path.GetDirectoryName(path);
			if (!fileSystem.Directory.Exists(dirPath))
			{
				fileSystem.Directory.CreateDirectory(dirPath);
			}

			if (overwrite)
			{
				if (fileSystem.File.Exists(path))
				{
					fileSystem.File.Delete(path);
				}
			}
			if (loadVariables && !string.IsNullOrEmpty(content))
			{
				content = variableHelper.LoadVariables(content, emptyVariableIfNotFound).ToString();
			}
			await fileSystem.File.WriteAllTextAsync(path, content, encoding: Encoding.GetEncoding(encoding));
		}

		public async Task AppendToFile(string path, string content, string? seperator = null, 
				bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			path = GetPath(path);
			string dirPath = Path.GetDirectoryName(path);
			if (!fileSystem.Directory.Exists(dirPath))
			{
				fileSystem.Directory.CreateDirectory(dirPath);
			}
			if (loadVariables && !string.IsNullOrEmpty(content))
			{
				content = variableHelper.LoadVariables(content, emptyVariableIfNotFound).ToString();
			}
			await fileSystem.File.AppendAllTextAsync(path, content + seperator, encoding: Encoding.GetEncoding(encoding));
		}

		public async Task CopyFiles(string directoryPath, string destinationPath, string searchPattern = "*", string[]? excludePatterns = null,
			bool includeSubfoldersAndFiles = false, bool overwriteFiles = false)
		{
			directoryPath = directoryPath.AdjustPathToOs();
			bool isAppFolder = false;
			if (directoryPath.StartsWith("."))
			{
				isAppFolder = true;
			}

			var files = await GetFilePathsInDirectory(directoryPath, searchPattern, excludePatterns, includeSubfoldersAndFiles);
			foreach (var file in files)
			{
				var copyDestinationFilePath = (isAppFolder) ? Path.Join(destinationPath, file) : file.Replace(directoryPath, destinationPath);
				await CopyFile(file, copyDestinationFilePath, true, overwriteFiles);
			}

		}

		public async Task CopyFile(string sourceFileName, string destFileName, bool createDirectoryIfNotExisting = false, bool overwriteFile = false)
		{
			sourceFileName = GetPath(sourceFileName);
			destFileName = GetPath(destFileName);
			if (createDirectoryIfNotExisting && !fileSystem.Directory.Exists(Path.GetDirectoryName(destFileName)))
			{
				fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(destFileName));
			}
			fileSystem.File.Copy(sourceFileName, destFileName, overwriteFile);
		}
		public async Task DeleteFile(string fileName, bool throwErrorOnNotFound = false)
		{
			fileName = GetPath(fileName);
			if (fileSystem.File.Exists(fileName))
			{
				fileSystem.File.Delete(fileName);
			}
			else if (throwErrorOnNotFound)
			{
				throw new FileNotFoundException($"{fileName} could not be found");
			}
		}
		public async Task<IFileInfo> GetFileInfo(string fileName)
		{
			fileName = GetPath(fileName);
			return fileSystem.FileInfo.New(fileName);
		}

		public async Task CreateDirectory(string directoryPath)
		{
			directoryPath = GetPath(directoryPath);
			if (!fileSystem.Directory.Exists(directoryPath))
			{
				fileSystem.Directory.CreateDirectory(directoryPath);
			}
		}
		public async Task DeleteDirectory(string directoryPath, bool recursive = true, bool throwErrorOnNotFound = false)
		{
			directoryPath = GetPath(directoryPath);
			if (fileSystem.Directory.Exists(directoryPath))
			{
				fileSystem.Directory.Delete(directoryPath, recursive);
			}
			else if (throwErrorOnNotFound)
			{
				throw new DirectoryNotFoundException($"{directoryPath} does not exist");
			}
		}


		private ConcurrentDictionary<string, Timer> timers = new ConcurrentDictionary<string, Timer>();

		[Description("debounceTime is the time in ms that is waited until action is executed to prevent multiple execution for same file.")]
		public async Task ListenToFileChange(string[] fileSearchPatterns, string goalToCall, string[]? excludeFiles = null,
			bool includeSubdirectories = false, long debounceTime = 150)
		{
			PLangFileSystemWatcherFactory watcherFactory = new PLangFileSystemWatcherFactory(fileSystem);
			foreach (var fileSearchPattern in fileSearchPatterns)
			{
				if (Path.IsPathFullyQualified(fileSearchPattern) && !fileSearchPattern.StartsWith(fileSystem.GoalsPath))
				{
					throw new RuntimeStepException("fileSearchPattern is out of app folder. You can only listen for files inside same app folder", goalStep);
				}

				var watcher = watcherFactory.New();
				watcher.Path = fileSystem.GoalsPath;
				watcher.IncludeSubdirectories = includeSubdirectories;
				watcher.Filter = fileSearchPattern;

				watcher.Changed += (object sender, FileSystemEventArgs e) =>
				{
					AddEventToTimer(sender, e, debounceTime, goalToCall, excludeFiles);

				};
				watcher.Created += (object sender, FileSystemEventArgs e) =>
				{
					AddEventToTimer(sender, e, debounceTime, goalToCall, excludeFiles);

				};
				watcher.Deleted += async (object sender, FileSystemEventArgs e) =>
				{
					AddEventToTimer(sender, e, debounceTime, goalToCall, excludeFiles);

				};
				watcher.Renamed += async (object sender, RenamedEventArgs e) =>
				{
					Timer? timer;
					if (timers.TryGetValue(e.FullPath, out timer))
					{
						timer.Change(debounceTime, Timeout.Infinite);
					}
					else
					{
						timer = new Timer((state) => {
							if (excludeFiles != null && excludeFiles.Contains(e.Name)) return;

							Dictionary<string, object> parameters = new Dictionary<string, object>();
							parameters.Add("OldFullPath", e.OldFullPath);
							parameters.Add("OldName", e.OldName);
							parameters.Add("FullPath", e.FullPath);
							parameters.Add("Name", e.Name);
							parameters.Add("ChangeType", e.ChangeType);
							parameters.Add("Sender", sender);

							var task = pseudoRuntime.RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), goalToCall, parameters);
							task.Wait();
						}, e.FullPath, debounceTime, Timeout.Infinite);
						timers.TryAdd(e.FullPath, timer);
					}
					
				};

				watcher.EnableRaisingEvents = true;

				int counter = 0;
				while (context.ContainsKey($"FileWatcher_{fileSearchPattern}_{goalToCall}_{counter}")) { counter++; };

				context.Add($"FileWatcher_{fileSearchPattern}_{goalToCall}_{counter}", watcher);
				KeepAlive(this, $"FileWatcher [{fileSearchPattern}]");
			}
		}

		private void AddEventToTimer(object sender, FileSystemEventArgs e, long debounceTime, string goalToCall, string[]? excludeFiles)
		{
			Timer? timer;
			if (timers.TryGetValue(e.FullPath, out timer))
			{
				timer.Change(debounceTime, Timeout.Infinite);
			}
			else
			{
				timer = new Timer((state) => { 
					WatcherCallGoal(sender, e, goalToCall, excludeFiles); 
				}, e.FullPath, debounceTime, Timeout.Infinite);
				timers.TryAdd(e.FullPath, timer);
			}
		}

		private static readonly object _lock = new object();
		private void WatcherCallGoal(object sender, FileSystemEventArgs e, string goalToCall, string[]? excludeFiles)
		{
			if (excludeFiles != null && excludeFiles.Contains(e.Name)) return;

			lock (_lock)
			{
				Dictionary<string, object> parameters = new Dictionary<string, object>();
				parameters.Add("FullPath", e.FullPath);
				parameters.Add("Name", e.Name);
				parameters.Add("ChangeType", e.ChangeType);
				parameters.Add("Sender", sender);

				try
				{
					var task = pseudoRuntime.RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), goalToCall, parameters);
					task.Wait();
				} catch (Exception ex) {
					logger.LogError(ex, goalStep.Text);
				}
			}
		}


		public void Dispose()
		{
			var fileStreams = context.Keys.Where(p => p.StartsWith("FileStream_"));
			foreach (var key in fileStreams)
			{
				((FileStream)context[key]).Dispose();
			}
			
			var fileWatchers = context.Keys.Where(p => p.StartsWith("FileWatcher_"));
			foreach (var key in fileWatchers)
			{
				((IFileSystemWatcher)context[key]).Dispose();
			}
		}
	}
}
