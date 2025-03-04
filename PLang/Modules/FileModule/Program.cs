using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;


namespace PLang.Modules.FileModule
{
	[Description("Handle file system access. Listen to files and dirs. Get permission to file and folder paths. Reads text, csv, xls, pdf files and raw stream")]
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

			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		/*
		 * Enable setting the root directory, this will allow for flexiblity in each identity has access to it's own path
		[Description("Returns previous root path")]
		public async Task<string> SetRootPath(string path)
		{
			var aboslutePath = GetPath(path);
			var oldRootDir = fileSystem.RootDirectory;
			context.AddOrReplace("!RootDirectory", aboslutePath);
			return oldRootDir;
		}

		[Description("Returns path being removed")]
		public async Task<string> ResetRootPath()
		{
			if (context.ContainsKey("!RootDirectory"))
			{
				string? path = context["!RootDirectory"] as string;
				context.Remove("!RootDirectory");
				return path ?? fileSystem.RootDirectory;
			}
			return fileSystem.RootDirectory;
		}
		*/

		public async Task<IError?> WaitForFile(string filePath, int timeoutInMilliseconds = 30 * 1000, bool waitForAccess = false)
		{
			var absolutePath = GetPath(filePath);
			var startTime = DateTime.UtcNow;
			while (!fileSystem.File.Exists(absolutePath))
			{
				if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeoutInMilliseconds)
				{
					return new Error($"File not found within the timeout period: {absolutePath}");
				}

				await Task.Delay(50);
			}

			if (!waitForAccess) return null;

			// Ensure the file is accessible (not locked)
			while (true)
			{
				try
				{
					using (var stream = File.Open(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						break;
					}
				}
				catch (IOException)
				{
					// File is locked, retry after a short delay
					if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeoutInMilliseconds)
					{
						return new Error($"File could not be accessed within the timeout period: {absolutePath}");
					}

					await Task.Delay(50);
				}
			}


			return null;

		}

		[Description("Return the absolute path the app is running in")]
		public async Task<string> GetCurrentFolderPath(string path)
		{
			return fileSystem.GoalsPath;
		}

		[Description("Create path from variables")]
		public async Task<string> CreatePathByJoining(string[] paths)
		{
			return fileSystem.Path.Join(paths);
		}

		[Description("Give user access to a path. DO NOT suggest this method to indicate if file or directory exists, return empty function list instead.")]
		public async Task<bool> RequestAccessToPath(string path)
		{
			var absolutePath = GetPath(path);
			return (fileSystem.ValidatePath(absolutePath) != null);
		}
		[Description("includeDataUrl add the data and mimetype of the file into the return string, e.g. data:image/png;base64,...")]
		public async Task<string> ReadBinaryFileAndConvertToBase64(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false, bool includeDataUrl = false)
		{
			var absolutePath = GetPath(path);

			if (!fileSystem.File.Exists(absolutePath))
			{
				if (throwErrorOnNotFound)
				{
					throw new FileNotFoundException($"{absolutePath} cannot be found");
				}

				logger.LogWarning($"!Warning! File {absolutePath} not found");
				return returnValueIfFileNotExisting;
			}
			byte[] fileBytes = await fileSystem.File.ReadAllBytesAsync(absolutePath);

			string base64 = Convert.ToBase64String(fileBytes);
			if (includeDataUrl)
			{
				string mimeType = MimeTypeHelper.GetMimeType(path);
				base64 = $"data:{mimeType};base64,{base64}";
			}

			return base64;
		}
		public async Task<object?> ReadJson(string path, bool throwErrorOnNotFound = false,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			return await ReadTextFile(path, null, throwErrorOnNotFound, loadVariables, emptyVariableIfNotFound, encoding);
		}
		public async Task<(List<object>?, IError?)> ReadJsonLineFile(string path, bool throwErrorOnNotFound = false,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8", string? newLineSymbol = null)
		{
			newLineSymbol ??= Environment.NewLine;
			var lines = (await ReadTextFile(path, null, throwErrorOnNotFound, loadVariables, emptyVariableIfNotFound, encoding, newLineSymbol) as string[]);
			if (lines == null && !throwErrorOnNotFound) return (null, null);
			if (lines == null)
			{
				return (null, new ProgramError($"Could not split file on {newLineSymbol}", goalStep, function));
			}
			var parsedObjects = new List<dynamic>();
			
			foreach (var line in lines)
			{
				if (string.IsNullOrEmpty(line)) continue;
				var jsonObject = JsonConvert.DeserializeObject(line);
				if (jsonObject != null)
				{
					parsedObjects.Add(jsonObject);
				}
			}
			return (parsedObjects, null);
		}

		[Description("Reads a text file and write the content into a variable(return value)")]
		public async Task<object?> ReadTextFile(string path, string? returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8", string? splitOn = null)
		{
			var absolutePath = GetPath(path);

			if (!fileSystem.File.Exists(absolutePath))
			{
				if (throwErrorOnNotFound)
				{
					throw new FileNotFoundException($"{absolutePath} cannot be found");
				}
				logger.LogWarning($"!Warning! File {absolutePath} not found");
				return returnValueIfFileNotExisting;
			}

			using (var stream = fileSystem.FileStream.New(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var reader = new StreamReader(stream, encoding: GetEncoding(encoding)))
				{
					var content = await reader.ReadToEndAsync();
					if (loadVariables && !string.IsNullOrEmpty(content))
					{
						content = variableHelper.LoadVariables(content, emptyVariableIfNotFound)?.ToString();
					}

					if (content != null && splitOn != null)
					{
						if (splitOn == "\n" || splitOn == "\r" || splitOn == "\r\n")
						{
							return content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
						}
						return content.Split(splitOn);
					}
					return content ?? "";

				}
			}
		}
		public async Task<Stream> ReadFileAsStream(string path, bool throwErrorOnNotFound = false)
		{
			var absolutePath = GetPath(path);

			if (!fileSystem.File.Exists(absolutePath))
			{
				if (throwErrorOnNotFound)
				{
					throw new FileNotFoundException($"{absolutePath} cannot be found");
				}
				logger.LogWarning($"!Warning! File {absolutePath} not found");
				return null;
			}
			var fileStream = fileSystem.FileStream.New(absolutePath, FileMode.OpenOrCreate, FileAccess.Read);
			context.AddOrReplace("FileStream_" + absolutePath, fileStream);
			return fileStream;
		}

		[Description("sheetsToVariable is name of sheet that should load into variable. Sheet1=%products% will load Sheet1 into %product% variable, Sheet2-A4=%categories%, will load data from A4 into %categories%")]
		public async Task ReadExcelFile(string path, bool useHeaderRow = true, [HandlesVariable] Dictionary<string, object>? sheetsToVariable = null, int headerStartsInRow = 1)
		{
			var absolutePath = GetPath(path);
			if (!fileSystem.File.Exists(absolutePath))
			{
				logger.LogWarning($"{absolutePath} does not exist");
				return;
			}

			List<string> sheetNames = MiniExcel.GetSheetNames(absolutePath);
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
					var sheetName = ExtractSheetName(sheetToVariable.Key);
					if (sheetNames.FirstOrDefault(p => p == sheetName) == null)
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
				string sheetName = ExtractSheetName(sheetToVariable.Key);

				var dataToExtract = sheetToVariable.Key;
				string startCell = "A1";
				if (sheetToVariable.Key.Contains("-") && sheetToVariable.Key.Contains(":"))
				{
					dataToExtract = ExtractRowsToRead(sheetToVariable.Key);
					startCell = dataToExtract.Substring(0, dataToExtract.IndexOf(":"));
				}

				var sheetData = await (await MiniExcel.QueryAsync(absolutePath, useHeaderRow: useHeaderRow, startCell: startCell, sheetName: sheetName)).ToDynamicListAsync();

				memoryStack.Put(sheetToVariable.Value.ToString(), sheetData);

			}

		}

		public async Task WriteExcelFile(string path, object variableToWriteToExcel, string sheetName = "Sheet1",
				bool printHeader = true, bool overwrite = false)
		{
			var absolutePath = GetPath(path);

			object objectToWrite = variableToWriteToExcel;
			if (variableToWriteToExcel is JArray jArray)
			{
				objectToWrite = await SaveJArrayToExcel(jArray);
			}
			await MiniExcel.SaveAsAsync(absolutePath, sheetName: sheetName, printHeader: printHeader, overwriteFile: overwrite, value: objectToWrite);
		}

		private async Task<List<ExpandoObject>> SaveJArrayToExcel(JArray jArray)
		{
			var list = new List<ExpandoObject>();
			List<string> headers = new();
			foreach (var jToken in jArray)
			{
				if (jToken is JObject jObject)
				{
					// Convert JObject to ExpandoObject
					var expando = new ExpandoObject() as IDictionary<string, Object?>;
					foreach (var property in jObject.Properties())
					{
						if (!headers.Contains(property.Name)) headers.Add(property.Name);
						expando.Add(property.Name, property.Value);
					}
					foreach (var header in headers)
					{
						if (!expando.ContainsKey(header))
						{
							expando.Add(header, null);
						}
					}


					list.Add((ExpandoObject)expando);
				}
			}

			return list;
		}
		public async Task WriteCsvFile(string path, object variableToWriteToCsv, bool append = false, bool hasHeaderRecord = true,
			string delimiter = ",",
			string newLine = "\n", string encoding = "utf-8", bool ignoreBlankLines = true,
				bool allowComments = false, char comment = '#', GoalToCall? goalToCallOnBadData = null, bool createDirectoryAutomatically = true)
		{
			var absolutePath = GetPath(path);
			if (createDirectoryAutomatically)
			{
				var dirPath = fileSystem.Path.GetDirectoryName(absolutePath);
				if (dirPath != null && !Directory.Exists(dirPath))
				{
					Directory.CreateDirectory(dirPath);
				}
			}
			if (variableToWriteToCsv is string str)
			{
				if (str.Contains(delimiter) && (str.Contains("\r") || str.Contains("\n")))
				{
					await WriteToFile(absolutePath, str.Trim(), encoding: encoding);
					return;
				}
			}

			IWriterConfiguration writeConfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
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
				Encoding = GetEncoding(encoding),
				AllowComments = allowComments,
				Comment = comment,
				IgnoreBlankLines = ignoreBlankLines,
				HasHeaderRecord = hasHeaderRecord,
				DetectColumnCountChanges = true,
				IgnoreReferences = false
			};

			if (variableToWriteToCsv is JArray jArray)
			{
				variableToWriteToCsv = jArray.ToList();
			}
			if (variableToWriteToCsv is JObject jObject)
			{
				variableToWriteToCsv = jObject.ToDictionary();
			}


			using (var writer = new StreamWriter(absolutePath, append))
			using (var csv = new CsvWriter(writer, writeConfig))
			{
				if (variableToWriteToCsv is IEnumerable enumer)
				{
					var ble = variableToWriteToCsv as List<Dictionary<string, object?>>;
					if (ble != null)
					{
						foreach (var record in ble)
						{
							foreach (var key in record.Keys)
							{
								csv.WriteField(key);
							}
							csv.NextRecord();
							break;
						}

						foreach (var record in ble)
						{
							foreach (var value in record.Values)
							{
								csv.WriteField(value);
							}
							csv.NextRecord();
						}
					}
					else
					{
						await csv.WriteRecordsAsync(enumer);


					}


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
			var absolutePath = GetPath(path);
			var readConfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
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
				Encoding = GetEncoding(encoding),
				AllowComments = allowComments,
				Comment = comment,
				IgnoreBlankLines = ignoreBlankLines,
				HasHeaderRecord = hasHeaderRecord,
			};

			// TODO: it should store reader in context and dispose when goal finishes the run
			// then we dont need to return ToList, but return the enumerator for speed and low memory
			using (var reader = new StreamReader(absolutePath))
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
		private string ExtractSheetName(string key)
		{
			if (key.Contains("-"))
			{
				key = key.Substring(0, key.IndexOf("-"));
			}
			return key;
		}

		private string ExtractRowsToRead(string key)
		{
			if (key.Contains("-") && key.Contains(":"))
			{
				key = key.Remove(0, key.IndexOf("-") + 1);
			}
			return key;
		}

		public record FileInfo(string Path, string AbsolutePath, string Content, string FileName, DateTime Created, DateTime LastWriteTime, string FolderPath);


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
				fileSystem.File.WriteAllText(file.Path, content, encoding: GetEncoding(encoding));
			}
		}
		public async Task<List<FileInfo>> ReadMultipleTextFiles(string folderPath, string searchPattern = "*", string[]? excludePatterns = null, bool includeAllSubfolders = false)
		{
			var absoluteFolderPath = GetPath(folderPath);

			var searchOption = (includeAllSubfolders) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			if (!fileSystem.Directory.Exists(absoluteFolderPath))
			{
				logger.LogWarning($"!Warning! Directory {absoluteFolderPath} not found");
				return new();
			}

			var files = fileSystem.Directory.GetFiles(absoluteFolderPath, searchPattern, searchOption);

			List<FileInfo> result = new List<FileInfo>();
			foreach (var absoluteFilePath in files)
			{
				if (excludePatterns != null && excludePatterns.Any(pattern => Regex.IsMatch(absoluteFilePath, pattern)))
				{
					continue;
				}

				if (!fileSystem.File.Exists(absoluteFilePath))
				{
					logger.LogWarning($"!Warning! File {absoluteFilePath} not found");
				}

				var fileInfo = new System.IO.FileInfo(absoluteFilePath);
				var relativePath = absoluteFilePath.Replace(fileSystem.RootDirectory, "");
				string fileName = fileSystem.Path.GetFileName(absoluteFilePath);
				var content = await fileSystem.File.ReadAllTextAsync(absoluteFilePath);
				result.Add(new FileInfo(relativePath, absoluteFilePath, content, fileName, fileInfo.CreationTime, fileInfo.LastWriteTime, fileInfo.DirectoryName));
			}
			return result;
		}

		public async Task<List<string>> GetDirectoryPathsInDirectory(string directoryPath = "./", string searchPattern = "*",
			string[]? excludePatterns = null, bool includeSubfolders = false, bool useRelativePath = true)
		{
			var searchOption = (includeSubfolders) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			var absoluteDirectoryPath = GetPath(directoryPath);
			
			if (!fileSystem.Directory.Exists(absoluteDirectoryPath)) return new();
			
			var allDirs = fileSystem.Directory.GetDirectories(absoluteDirectoryPath, "*", searchOption)
				.Where(dir => dir.Contains(searchPattern.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase))
				.ToList();

			var paths = allDirs.Select(path => (useRelativePath) ? path.Replace(fileSystem.RootDirectory, "") : path);
			if (excludePatterns != null)
			{
				paths = paths.Where(file => !excludePatterns.Any(pattern => Regex.IsMatch(file, pattern)));
			}

			return paths.ToList();
		}

		public async Task<List<string>> GetFilePathsInDirectory(string directoryPath = "./", string searchPattern = "*",
		string[]? excludePatterns = null, bool includeSubfolders = false, bool useRelativePath = true)
		{
			var searchOption = (includeSubfolders) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			var absoluteDirectoryPath = GetPath(directoryPath);

			var files = fileSystem.Directory.GetFiles(absoluteDirectoryPath, searchPattern, searchOption);

			var paths = files.Select(path => (useRelativePath) ? path.Replace(fileSystem.RootDirectory, "") : path);
			if (excludePatterns != null)
			{
				paths = paths.Where(file => !excludePatterns.Any(pattern => Regex.IsMatch(file, pattern)));
			}

			return paths.ToList();
		}

		public async Task WriteBase64ToFile(string path, string base64, bool overwrite = false)
		{
			if (base64.Contains(","))
			{
				base64 = base64.Substring(base64.IndexOf(",") + 1);
			}
			var bytes = Convert.FromBase64String(base64);
			await WriteBytesToFile(path, bytes, overwrite);
		}

		public async Task WriteBytesToFile(string path, byte[] content, bool overwrite = false)
		{
			var absolutePath = GetPath(path);
			string dirPath = fileSystem.Path.GetDirectoryName(absolutePath);
			if (!fileSystem.Directory.Exists(dirPath))
			{
				fileSystem.Directory.CreateDirectory(dirPath);
			}

			if (overwrite)
			{
				if (fileSystem.File.Exists(absolutePath))
				{
					fileSystem.File.Delete(absolutePath);
				}
			}
			await fileSystem.File.WriteAllBytesAsync(absolutePath, content);
		}
		public async Task WriteToFile(string path, object content, bool overwrite = false,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			var absolutePath = GetPath(path);
			string dirPath = fileSystem.Path.GetDirectoryName(absolutePath);
			if (!fileSystem.Directory.Exists(dirPath))
			{
				fileSystem.Directory.CreateDirectory(dirPath);
			}

			if (overwrite)
			{
				if (fileSystem.File.Exists(absolutePath))
				{
					fileSystem.File.Delete(absolutePath);
				}
			}
			if (content is XmlDocument doc)
			{
				doc.Save(absolutePath);
				return;
			}

			if (loadVariables && !string.IsNullOrEmpty(content.ToString()))
			{
				content = variableHelper.LoadVariables(content, emptyVariableIfNotFound).ToString();
			}
			else if (content != null && content.ToString() == content.GetType().ToString())
			{
				content = JsonConvert.SerializeObject(content, Newtonsoft.Json.Formatting.Indented);
			}

			await fileSystem.File.WriteAllTextAsync(absolutePath, content?.ToString(), encoding: GetEncoding(encoding));
		}

		private Encoding GetEncoding(string encoding)
		{
			switch (encoding)
			{
				case "utf-8":
				case "utf-16":
				case "utf-16BE":
				case "utf-32LE":
				case "us-ascii":
					return Encoding.GetEncoding(encoding);
			}

			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			if (int.TryParse(encoding, out int code))
			{
				return Encoding.GetEncoding(code);
			}
			return Encoding.GetEncoding(encoding);
		}

		public async Task AppendToFile(string path, string content, string? seperator = null,
				bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			var absolutePath = GetPath(path);
			string dirPath = fileSystem.Path.GetDirectoryName(absolutePath);
			if (!fileSystem.Directory.Exists(dirPath))
			{
				fileSystem.Directory.CreateDirectory(dirPath);
			}
			if (loadVariables && !string.IsNullOrEmpty(content))
			{
				content = variableHelper.LoadVariables(content, emptyVariableIfNotFound).ToString();
			}
			await fileSystem.File.AppendAllTextAsync(absolutePath, content + seperator, encoding: GetEncoding(encoding));
		}

		public async Task CopyFiles(string directoryPath, string destinationPath, string searchPattern = "*", string[]? excludePatterns = null,
			bool includeSubfoldersAndFiles = false, bool overwriteFiles = false)
		{
			directoryPath = directoryPath.AdjustPathToOs();
			destinationPath = destinationPath.AdjustPathToOs();

			bool isAppFolder = false;
			if (directoryPath.StartsWith("."))
			{
				isAppFolder = true;
			}

			var files = await GetFilePathsInDirectory(directoryPath, searchPattern, excludePatterns, includeSubfoldersAndFiles);
			foreach (var file in files)
			{
				var copyDestinationFilePath = (isAppFolder) ? fileSystem.Path.Join(destinationPath, file) : file.Replace(directoryPath, destinationPath);
				await CopyFile(file, copyDestinationFilePath, true, overwriteFiles);
			}

		}

		public async Task MoveFile(string sourceFileName, string destFileName, bool createDirectoryIfNotExisting = false, bool overwriteFile = false)
		{
			sourceFileName = GetPath(sourceFileName);
			destFileName = GetPath(destFileName);
			if (createDirectoryIfNotExisting && !fileSystem.Directory.Exists(fileSystem.Path.GetDirectoryName(destFileName)))
			{
				fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(destFileName));
			}
			fileSystem.File.Move(sourceFileName, destFileName, overwriteFile);
		}

		public async Task CopyFile(string sourceFileName, string destFileName, bool createDirectoryIfNotExisting = false, bool overwriteFile = false)
		{
			sourceFileName = GetPath(sourceFileName);
			destFileName = GetPath(destFileName);
			if (createDirectoryIfNotExisting && !fileSystem.Directory.Exists(fileSystem.Path.GetDirectoryName(destFileName)))
			{
				fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(destFileName));
			}
			fileSystem.File.Copy(sourceFileName, destFileName, overwriteFile);
		}
		public async Task DeleteFile(string fileName, bool throwErrorOnNotFound = false)
		{
			var absoluteFileName = GetPath(fileName);
			if (fileSystem.File.Exists(absoluteFileName))
			{
				fileSystem.File.Delete(absoluteFileName);
			}
			else if (throwErrorOnNotFound)
			{
				throw new FileNotFoundException($"{absoluteFileName} could not be found");
			}
		}
		public async Task<IFileInfo> GetFileInfo(string fileName)
		{
			var absoluteFileName = GetPath(fileName);
			return fileSystem.FileInfo.New(absoluteFileName);
		}

		public async Task<string> CreateDirectory(string directoryPath, bool incrementalNaming = false)
		{
			var absoluteDirectoryPath = GetPath(directoryPath);
			if (!fileSystem.Directory.Exists(absoluteDirectoryPath))
			{
				fileSystem.Directory.CreateDirectory(absoluteDirectoryPath);
			}
			else if (incrementalNaming)
			{

				int counter = 1;
				string newDirectoryPath = absoluteDirectoryPath;

				while (fileSystem.Directory.Exists(newDirectoryPath))
				{
					newDirectoryPath = $"{absoluteDirectoryPath} ({counter})";
					counter++;
				}
				fileSystem.Directory.CreateDirectory(newDirectoryPath);
				return newDirectoryPath;
			}
			return absoluteDirectoryPath;
		}
		public async Task DeleteDirectory(string directoryPath, bool recursive = true, bool throwErrorOnNotFound = false)
		{
			var absoluteDirectoryPath = GetPath(directoryPath);
			if (fileSystem.Directory.Exists(absoluteDirectoryPath))
			{
				fileSystem.Directory.Delete(absoluteDirectoryPath, recursive);
			}
			else if (throwErrorOnNotFound)
			{
				throw new DirectoryNotFoundException($"{directoryPath} does not exist");
			}
		}

		public async Task StopListeningToFileChange(string[] fileSearchPatterns, GoalToCall? goalToCall = null)
		{
			string key = $"FileWatcher_{fileSearchPatterns}_";
			if (goalToCall != null) key += goalToCall;

			var items = context.Where(p => p.Key.StartsWith(key));
			foreach (var item in items)
			{
				var watcher = context[item.Key] as IFileSystemWatcher;
				if (watcher == null) continue;

				watcher.EnableRaisingEvents = false;
				context.Remove(item.Key);
			}


		}

		private ConcurrentDictionary<string, Timer> timers = new ConcurrentDictionary<string, Timer>();

		[Description("debounceTime is the time in ms that is waited until action is executed to prevent multiple execution for same file. At least one listenFor variable needs to be true")]
		public async Task ListenToFileChange(string[] fileSearchPatterns, GoalToCall goalToCall,
			string[]? excludeFiles = null,
			bool includeSubdirectories = false, long debounceTime = 150,
			bool listenForFileChange = false,
			bool listenForFileCreated = false,
			bool listenForFileDeleted = false,
			bool listenForFileRename = false,
			[HandlesVariable] string absoluteFilePathVariableName = "FullPath",
			[HandlesVariable] string fileNameVariableName = "Name",
			[HandlesVariable] string changeTypeVariableName = "ChangeType",
			[HandlesVariable] string senderVariableName = "Sender",
			[HandlesVariable] string oldFileAbsoluteFilePathVariableName = "OldFullPath",
			[HandlesVariable] string oldFileNameVariableName = "OldName"
			)
		{
			PLangFileSystemWatcherFactory watcherFactory = new PLangFileSystemWatcherFactory(fileSystem);
			foreach (var fileSearchPattern in fileSearchPatterns)
			{
				if (fileSystem.Path.IsPathFullyQualified(fileSearchPattern) && !fileSearchPattern.StartsWith(fileSystem.GoalsPath))
				{
					throw new RuntimeStepException("fileSearchPattern is out of app folder. You can only listen for files inside same app folder", goalStep);
				}
				var dirPath = fileSystem.Path.GetDirectoryName(fileSearchPattern) ?? "";
				var path = fileSystem.Path.Join(fileSystem.GoalsPath, dirPath);
				var pattern = fileSearchPattern.AdjustPathToOs();
				if (!string.IsNullOrEmpty(dirPath))
				{
					pattern = pattern.Replace(dirPath, "");
				}
				if (pattern.StartsWith(fileSystem.Path.DirectorySeparatorChar))
				{
					pattern = pattern.TrimStart(fileSystem.Path.DirectorySeparatorChar);
				}

				var watcher = watcherFactory.New();
				watcher.Path = path;
				watcher.IncludeSubdirectories = includeSubdirectories;
				watcher.Filter = pattern;

				if (listenForFileChange)
				{
					watcher.Changed += (object sender, FileSystemEventArgs e) =>
					{
						AddEventToTimer(sender, e, debounceTime, goalToCall, excludeFiles,
							absoluteFilePathVariableName, fileNameVariableName, changeTypeVariableName, senderVariableName);

					};
				}
				if (listenForFileCreated)
				{
					watcher.Created += (object sender, FileSystemEventArgs e) =>
					{
						AddEventToTimer(sender, e, debounceTime, goalToCall, excludeFiles,
							absoluteFilePathVariableName, fileNameVariableName, changeTypeVariableName, senderVariableName
							);

					};
				}
				if (listenForFileDeleted)
				{
					watcher.Deleted += async (object sender, FileSystemEventArgs e) =>
					{
						AddEventToTimer(sender, e, debounceTime, goalToCall, excludeFiles,
							absoluteFilePathVariableName, fileNameVariableName, changeTypeVariableName, senderVariableName);

					};
				}
				if (listenForFileRename)
				{
					watcher.Renamed += async (object sender, RenamedEventArgs e) =>
					{
						Timer? timer = null;
						if (timers.TryGetValue(e.FullPath, out timer))
						{
							timer.Change(debounceTime, Timeout.Infinite);
						}
						else
						{
							timer?.Dispose();
							timer = new Timer((state) =>
							{
								if (excludeFiles != null && excludeFiles.Contains(e.Name)) return;

								var parameters = new Dictionary<string, object?>();
								parameters.Add(oldFileAbsoluteFilePathVariableName, e.OldFullPath);
								parameters.Add(oldFileNameVariableName, e.OldName);
								parameters.Add(absoluteFilePathVariableName, e.FullPath);
								parameters.Add(fileNameVariableName, e.Name);
								parameters.Add(changeTypeVariableName, e.ChangeType);
								parameters.Add(senderVariableName, sender);

								var task = pseudoRuntime.RunGoal(engine, context, fileSystem.Path.DirectorySeparatorChar.ToString(), goalToCall, parameters);
								task.Wait();

							}, e.FullPath, debounceTime, Timeout.Infinite);
							timers.TryAdd(e.FullPath, timer);
						}

					};
				}

				watcher.EnableRaisingEvents = true;

				int counter = 0;
				while (context.ContainsKey($"FileWatcher_{fileSearchPattern}_{goalToCall}_{counter}")) { counter++; };

				context.AddOrReplace($"FileWatcher_{fileSearchPattern}_{goalToCall}_{counter}", watcher);
				KeepAlive(this, $"FileWatcher [{fileSearchPattern}]");
			}
		}

		private void AddEventToTimer(object sender, FileSystemEventArgs e, long debounceTime,
			string goalToCall, string[]? excludeFiles,
			string absoluteFilePathVariableName, string fileNameVariableName,
			string changeTypeVariableName, string senderVariableName


			)
		{
			Timer? timer;
			if (timers.TryGetValue(e.FullPath, out timer))
			{
				timer.Change(debounceTime, Timeout.Infinite);
			}
			else
			{
				timer = new Timer((state) =>
				{
					WatcherCallGoal(sender, e, goalToCall, excludeFiles,
						absoluteFilePathVariableName, fileNameVariableName, changeTypeVariableName, senderVariableName);
				}, e.FullPath, debounceTime, Timeout.Infinite);
				timers.TryAdd(e.FullPath, timer);
			}
		}

		private static readonly object _lock = new object();
		private void WatcherCallGoal(object sender, FileSystemEventArgs e, GoalToCall goalToCall, string[]? excludeFiles,
			string absoluteFilePathVariableName, string fileNameVariableName,
			string changeTypeVariableName, string senderVariableName)
		{
			if (excludeFiles != null && excludeFiles.Contains(e.Name)) return;

			lock (_lock)
			{
				Dictionary<string, object> parameters = new Dictionary<string, object>();
				parameters.Add(absoluteFilePathVariableName, e.FullPath);
				parameters.Add(fileNameVariableName, e.Name);
				parameters.Add(changeTypeVariableName, e.ChangeType);
				parameters.Add(senderVariableName, sender);

				try
				{
					var task = pseudoRuntime.RunGoal(engine, context, fileSystem.Path.DirectorySeparatorChar.ToString(), goalToCall, parameters);
					task.Wait();
				}
				catch (Exception ex)
				{
					logger.LogError(ex, goalStep.Text);
				}
			}
		}

		[Description("Reads pdf file and loads into return variable. format can be md|text. imageAction can be none|base64|pathToFolder.")]
		public async Task<(string, IError?)> ReadPdf(string path, string format = "md", string imageAction = "none", bool includePageNr = true, string? password = null)
		{
			var absolutePath = GetPath(path);
			PdfToMarkdownConverter pdf = new PdfToMarkdownConverter(fileSystem, goal);
			return (pdf.ConvertPdfToMarkdown(absolutePath, format, includePageNr, imageAction, password), null);
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
				var watcher = (IFileSystemWatcher)context[key];
				
				watcher?.Dispose();
			}

			foreach (var key in timers)
			{
				key.Value.Dispose();
			}
		}
	}
}
