using CsvHelper;
using CsvHelper.Configuration;
using Markdig;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.ThrowErrorModule;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using static PLang.Modules.FileModule.CsvHelper;


namespace PLang.Modules.FileModule
{
	[Description("Handle file system access. Listen to files and dirs. Get permission to file and folder paths. Reads files, such as text, llm, csv, xls, pdf files and raw stream")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IErrorSystemHandlerFactory errorSystemHandlerFactory;
		private readonly TypeMapping typeMapping;
		public Program(IPLangFileSystem fileSystem, ISettings settings,
			ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine, IFileAccessHandler fileAccessHandler, IErrorSystemHandlerFactory errorSystemHandlerFactory) : base()
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.logger = logger;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.fileAccessHandler = fileAccessHandler;
			this.errorSystemHandlerFactory = errorSystemHandlerFactory;
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			
			string typeMappingKey = "PLang.Modules.FileModule.TypeMapping";
			if (context == null)
			{
				this.typeMapping = new TypeMapping();
			} else if (context.TryGetValue<TypeMapping>(typeMappingKey, out var typeMapping))
			{
				typeMapping = new TypeMapping();
				context.AddOrReplace(typeMappingKey, typeMapping);
			}
		}

		public async Task GiveAccess(string path)
		{
			fileAccessHandler.GiveAccess(fileSystem.RootDirectory, path);
		}

		public async Task<(object?, IError?)> FileExists(string filePathOrVariableName, GoalToCallInfo? goalToCallIfTrue = null, GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			var condition = GetProgramModule<ConditionalModule.Program>();
			return await condition.FileExists(filePathOrVariableName, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse);
		}

		public async Task<IError?> WaitForFile(string filePath, int timeoutInMilliseconds = 30 * 1000, bool waitForAccess = false)
		{
			var absolutePath = GetPath(filePath);
			var startTime = DateTime.UtcNow;
			while (!fileSystem.File.Exists(absolutePath))
			{
				if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeoutInMilliseconds)
				{
					return new Error($"File not found within the timeout period: {absolutePath}", StatusCode: 404);
				}

				await Task.Delay(50);
			}

			if (!waitForAccess) return null;

			// Ensure the file is accessible (not locked)
			while (true)
			{
				try
				{
					using (var stream = fileSystem.File.Open(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
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
		public async Task<(object?, IError?)> ReadJson(string path, bool throwErrorOnNotFound = true,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8", bool allowReadingFromSystem = false)
		{
			return await ReadTextFile(path, null, throwErrorOnNotFound, loadVariables, emptyVariableIfNotFound, encoding, null, allowReadingFromSystem);
		}
		public async Task<(List<object>?, IError?)> ReadJsonLineFile(string path, bool throwErrorOnNotFound = true,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8", string? newLineSymbol = null, bool allowReadingFromSystem = false)
		{
			newLineSymbol ??= Environment.NewLine;
			var result = await ReadTextFile(path, null, throwErrorOnNotFound, loadVariables, emptyVariableIfNotFound, encoding, newLineSymbol, allowReadingFromSystem);
			var lines = result.Item1 as string[];

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
		public async Task<(object? Content, IError? Error)> ReadTextFile(string path, string? returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = true,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8", string? splitOn = null, bool allowReadingFromSystem = false)
		{
			var absolutePath = GetPath(path);

			if (!fileSystem.File.Exists(absolutePath))
			{
				if (allowReadingFromSystem || goal.IsSystem)
				{
					absolutePath = GetSystemPath(path);
				}

				if (!fileSystem.File.Exists(absolutePath))
				{
					if (throwErrorOnNotFound)
					{
						return (null, new ProgramError($"{absolutePath} cannot be found", goalStep, function, Key: "FileNotFound", StatusCode: 404));
					}
					if (IsDebugMode)
					{
						logger.LogWarning($"File not found. path: {path} | absolutePath:{absolutePath}");
					}
					return (returnValueIfFileNotExisting, null);
				}
			}

			using (var stream = fileSystem.FileStream.New(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var reader = new StreamReader(stream, encoding: FileHelper.GetEncoding(encoding)))
				{
					var content = await reader.ReadToEndAsync();
					if (loadVariables && !string.IsNullOrEmpty(content))
					{
						content = memoryStack.LoadVariables(content, emptyVariableIfNotFound)?.ToString();
					}

					if (content != null && splitOn != null)
					{
						if (splitOn == "\n" || splitOn == "\r" || splitOn == "\r\n")
						{
							return (content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None), null);
						}
						return (content.Split(splitOn), null);
					}
					return (content ?? returnValueIfFileNotExisting, null);

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
			appContext.AddOrReplace("FileStream_" + absolutePath, fileStream);
			return fileStream;
		}

		public record Sheet(string Name, string StartRow = "A1", string? VariableName = null, bool UseHeaderRow = true);

		[Description("sheetsToExtract is name of sheet that should load into variable, default is null and will read all sheets, When user defines a sheet property but know name, set Name of sheet to *. Sheet1=%products% will load Sheet1 into %product% variable. StartRow MUST contains letter and number, e.g. A1")]
		public async Task<object?> ReadExcelFile(string path,
			[HandlesVariable] List<Sheet>? sheetsToExtract = null)
		{
			var absolutePath = GetPath(path);
			if (!fileSystem.File.Exists(absolutePath))
			{
				logger.LogWarning($"{absolutePath} does not exist");
				return null;
			}


			if (sheetsToExtract == null || sheetsToExtract.Count == 0)
			{
				sheetsToExtract = new();
				List<string> sheetNames = MiniExcel.GetSheetNames(absolutePath);
				foreach (var sheetName in sheetNames)
				{
					sheetsToExtract.Add(new Sheet(sheetName, "A1", MakeFitForVariable(sheetName)));
				}
			}
			else if (string.IsNullOrEmpty(sheetsToExtract[0].Name) || sheetsToExtract[0].Name == "*")
			{
				List<Sheet> sheets = new();

				List<string> sheetNames = MiniExcel.GetSheetNames(absolutePath);
				foreach (var sheetName in sheetNames)
				{
					sheets.Add(new Sheet(sheetName, sheetsToExtract[0].StartRow, MakeFitForVariable(sheetName), sheetsToExtract[0].UseHeaderRow));
				}
				sheetsToExtract = sheets;
			}


			List<ObjectValue?> returnValues = new();
			foreach (var sheet in sheetsToExtract)
			{
				var newSheet = sheet;
				if (string.IsNullOrEmpty(sheet.VariableName))
				{
					newSheet = newSheet with { VariableName = MakeFitForVariable(sheet.Name) };
				}
				if (string.IsNullOrEmpty(sheet.StartRow))
				{
					newSheet = newSheet with { StartRow = "A1" };
				}
				var sheetData = await (await MiniExcel.QueryAsync(absolutePath, useHeaderRow: newSheet.UseHeaderRow, startCell: newSheet.StartRow, sheetName: newSheet.Name)).ToDynamicListAsync();

				returnValues.Add(new ObjectValue(newSheet.VariableName!, sheetData));

			}
			return returnValues;
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



		public async Task WriteCsvFile(string path, [HandlesVariable] object variableToWriteToCsv, bool appendToFile = false,
			CsvOptions? csvOptions = null, bool createDirectoryAutomatically = true)
		{
			if (csvOptions == null) csvOptions = new();

			Encoding? enc = Encoding.UTF8;
			if (VariableHelper.IsVariable(variableToWriteToCsv))
			{
				var ov = memoryStack.GetObjectValue(variableToWriteToCsv.ToString());
				var encoding = ov.Properties.FirstOrDefault(p => p.Name == "Encoding");
				if (encoding != null)
				{
					enc = encoding.ValueAs<Encoding>();
				}

				variableToWriteToCsv = ov.Value;
				if (enc != null)
				{
					if (csvOptions.Encoding == null)
					{
						csvOptions = csvOptions with { Encoding = enc.EncodingName };
					}
					else if (csvOptions.Encoding != enc.EncodingName)
					{
						var encodingTo = Encoding.GetEncoding(csvOptions.Encoding);
						var bytes = encodingTo.GetBytes(ov.Value.ToString());
						variableToWriteToCsv = encodingTo.GetString(bytes);
					}
				}
			}
			else
			{
				variableToWriteToCsv = memoryStack.LoadVariables(variableToWriteToCsv);
			}

			var absolutePath = GetPath(path);
			if (createDirectoryAutomatically)
			{
				var dirPath = fileSystem.Path.GetDirectoryName(absolutePath);
				if (dirPath != null && !fileSystem.Directory.Exists(dirPath))
				{
					fileSystem.Directory.CreateDirectory(dirPath);
				}
			}
			if (variableToWriteToCsv is string str)
			{
				if (str.Contains(csvOptions.Delimiter) && (str.Contains("\r") || str.Contains("\n")))
				{
					await WriteToFile(absolutePath, str.Trim(), encoding: csvOptions.Encoding);
					return;
				}
			}

			using var writer = new StreamWriter(absolutePath, appendToFile);
			await CsvHelper.WriteToStream(writer, variableToWriteToCsv, csvOptions);

		}
		public async Task<object> ReadCsvFile(string path, bool hasHeaderRecord = true, string delimiter = ",",
				string newLine = "\n", string encoding = "utf-8", bool ignoreBlankLines = true,
				bool allowComments = false, char comment = '#', GoalToCallInfo? goalToCallOnBadData = null)
		{
			var absolutePath = GetPath(path);
			var readConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
			{
				Delimiter = delimiter,
				BadDataFound = data =>
				{
					if (goalToCallOnBadData == null) return;

					Dictionary<string, object> parameters = new Dictionary<string, object>();
					goalToCallOnBadData.Parameters.Add("data", data);
					pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.RelativeAppPath, goalToCallOnBadData, Goal);
				},
				NewLine = newLine,
				Encoding = FileHelper.GetEncoding(encoding),
				AllowComments = allowComments,
				Comment = comment,
				IgnoreBlankLines = ignoreBlankLines,
				HasHeaderRecord = hasHeaderRecord,
			};

			// TODO: it should store reader in context and dispose when goal finishes the run
			// then we dont need to return ToList, but return the enumerator for speed and low memory
			using (var reader = new StreamReader(absolutePath, readConfig.Encoding))
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

		//public record FileInfo(string Path, string AbsolutePath, string Content, string FileName, DateTime Created, DateTime LastWriteTime, string FolderPath);


		public async Task SaveMultipleFiles(List<FileInfo> files, bool loadVariables = false,
			bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			foreach (var file in files)
			{
				var content = file.Content;
				if (content == null) continue;

				if (content is byte[] bytes)
				{
					fileSystem.File.WriteAllBytes(file.Path, bytes);
				}
				else
				{
					var text = content.ToString() ?? string.Empty;
					if (loadVariables && !string.IsNullOrEmpty(text))
					{
						text = memoryStack.LoadVariables(text, emptyVariableIfNotFound).ToString();
					}
					fileSystem.File.WriteAllText(file.Path, text, encoding: FileHelper.GetEncoding(encoding));
				}
			}
		}
		public async Task<(List<FileInfo>?, IError?)> ReadMultipleTextFiles(string folderPath, string searchPattern = "*", string[]? excludePatterns = null, bool includeAllSubfolders = false)
		{
			var absoluteFolderPath = GetPath(folderPath);
			var searchOption = includeAllSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			if (!fileSystem.Directory.Exists(absoluteFolderPath))
			{
				return (null, new ProgramError($"Directory {absoluteFolderPath} not found"));
			}

			var files = fileSystem.Directory.GetFiles(absoluteFolderPath, searchPattern, searchOption);
			var result = new List<FileInfo>();

			foreach (var absoluteFilePath in files)
			{
				if (excludePatterns != null && excludePatterns.Any(pattern => Regex.IsMatch(absoluteFilePath, pattern)))
				{
					continue;
				}

				if (!fileSystem.File.Exists(absoluteFilePath))
				{
					continue;
				}

				var fileInfo = fileSystem.FileInfo.New(absoluteFilePath);
				result.Add(FileInfo.FromFileInfo(fileInfo, fileSystem.RootDirectory, fileSystem, typeMapping));
			}

			return (result, null);
		}

		public record Directory(string Name, string Path, string AbsolutePath, Properties? DirectoryInfo = null);
		public async Task<List<Directory>> GetDirectoryPathsInDirectory(string directoryPath = "./", string? regexSearchPattern = null,
			string[]? excludePatterns = null, bool includeSubfolders = false, bool includeSystemFolder = false, bool includeDirectoryInfo = false)
		{
			var searchOption = (includeSubfolders) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			var absoluteDirectoryPath = GetPath(directoryPath);

			if (!fileSystem.Directory.Exists(absoluteDirectoryPath)) return new();

			var allDirs = fileSystem.Directory.GetDirectories(absoluteDirectoryPath, "*", searchOption);
			if (includeSystemFolder)
			{
				var systemPath = fileSystem.Path.Join(fileSystem.SystemDirectory, directoryPath);
				allDirs = allDirs.Concat(fileSystem.Directory.GetDirectories(systemPath, "*", searchOption)).ToArray();
			}
			if (!string.IsNullOrWhiteSpace(regexSearchPattern))
			{
				allDirs = allDirs.Where(dir => Regex.IsMatch(dir, regexSearchPattern)).ToArray();
			}

			var paths = allDirs.Select(path => path.Replace(fileSystem.RootDirectory, ""));
			if (excludePatterns != null)
			{
				paths = paths.Where(file => !excludePatterns.Any(pattern => Regex.IsMatch(file, pattern)));
			}

			List<Directory> dirs = new();
			foreach (var path in paths)
			{
				var absolutePath = fileSystem.Path.Join(fileSystem.RootDirectory, path);
				var name = path;
				if (path.Contains(fileSystem.Path.DirectorySeparatorChar))
				{
					name = name.Substring(path.LastIndexOf(fileSystem.Path.DirectorySeparatorChar) + 1);
				}
				DirectoryInfo? di = null;
				if (includeDirectoryInfo)
				{
					di = new DirectoryInfo(absolutePath);
				}
				dirs.Add(new Directory(name, path, absolutePath, GetProperties(di)));
			}

			return dirs;
		}

		private Properties? GetProperties(object? obj)
		{
			if (obj == null) return null;
			var properties = new Properties();
			foreach (var prop in obj.GetType().GetProperties())
			{
				if (TypeHelper.IsConsideredPrimitive(prop.PropertyType))
				{
					properties.Add(new ObjectValue(prop.Name, prop.GetValue(obj)));
				}
			}
			return properties;
		}


		public record File(string Name, string Extension, string Type, string Path, string AbsolutePath, Properties? FileInfo = null);

		[Description("excludePatterns is array of regex patterns, when matching with star, make sure to use .*")]
		public async Task<List<File>> GetFilePathsInDirectory(string directoryPath = "./", string searchPattern = "*",
		string[]? excludePatterns = null, bool includeSubfolders = false, bool includeFileInfo = false, string? filterOnType = null)
		{
			var searchOption = (includeSubfolders) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			var absoluteDirectoryPath = GetPath(directoryPath);

			var files = fileSystem.Directory.GetFiles(absoluteDirectoryPath, searchPattern, searchOption);

			var paths = files.Select(path => path.Replace(fileSystem.RootDirectory, ""));
			if (excludePatterns != null)
			{
				paths = paths.Where(file => !excludePatterns.Any(pattern =>
				{
					return Regex.IsMatch(file, pattern);
				}));
			}

			List<File> filesToReturn = new();
			foreach (var path in paths)
			{
				string extension = fileSystem.Path.GetExtension(path);
				var (type, error) = await GetFileType(extension);
				if (string.IsNullOrEmpty(type)) type = "unknown";

				if (filterOnType != null && !type.Equals(filterOnType, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				string absolutePath;
				string relativePath;
				if (fileSystem.IsOsRooted(path))
				{
					absolutePath = path;
					relativePath = path.Replace(directoryPath, "");
				}
				else
				{
					absolutePath = fileSystem.Path.Join(fileSystem.RootDirectory, path);
					relativePath = path;
				}
				var name = fileSystem.Path.GetFileName(path);

				System.IO.FileInfo? fi = null;
				if (includeFileInfo)
				{
					fi = new System.IO.FileInfo(absolutePath);
				}

				filesToReturn.Add(new File(name, extension, type, relativePath, absolutePath, GetProperties(fi)));
			}

			return filesToReturn;
		}

		public async Task<(string?, IError?)> GetFileType(string extension)
		{
			var fileTypes = context.GetVariable<Dictionary<string, string>>("FileTypes");

			if (!extension.StartsWith(".")) extension = "." + extension;

			return typeMapping.GetType(extension);

		}

		public async Task<(object?, IError?)> ReadXml(string path)
		{
			var result = await ReadTextFile(path);
			if (result.Error != null) return (null, result.Error);

			// todo: here we convert any xml to json so user can use JSONPath to get the content. 
			// better/faster would be to return the xml object, then when user wants to use json path, it uses xpath.
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(Regex.Replace(result.Content.ToString(), "<\\?xml.*?\\?>", "", RegexOptions.IgnoreCase));

			string jsonString = JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented, true);

			return (JsonConvert.DeserializeObject(jsonString), null);
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
		public async Task<IError?> WriteToFile(string path, object content, bool overwrite = false,
			bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")
		{
			if (string.IsNullOrEmpty(path))
			{
				return new ProgramError("Path cannot be empty.", FixSuggestion: "Make sure you are sending path in your step, if it's variable then it might be empty.");
			}

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
			if (content is IFormFile formFile)
			{
				using var stream = fileSystem.File.Create(absolutePath);
				await formFile.CopyToAsync(stream);
				return null;
			}
			if (content is XmlDocument doc)
			{
				doc.Save(absolutePath);
				return null;
			}

			if (loadVariables && !string.IsNullOrEmpty(content.ToString()))
			{
				content = memoryStack.LoadVariables(content, emptyVariableIfNotFound).ToString();
			}
			else if (content != null && content.ToString() == content.GetType().ToString())
			{
				content = JsonConvert.SerializeObject(content, Newtonsoft.Json.Formatting.Indented);
			}

			await fileSystem.File.WriteAllTextAsync(absolutePath, content?.ToString(), encoding: FileHelper.GetEncoding(encoding));
			return null;
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
				content = memoryStack.LoadVariables(content, emptyVariableIfNotFound).ToString();
			}
			await fileSystem.File.AppendAllTextAsync(absolutePath, content + seperator, encoding: FileHelper.GetEncoding(encoding));
		}

		public async Task CopyFiles(string directoryPath, string destinationPath, string searchPattern = "*", string[]? excludePatterns = null,
			bool includeSubfoldersAndFiles = false, bool overwriteFiles = false)
		{
			var absoluteDirectoryPath = GetPath(directoryPath);
			var absoluteDestinationPath = GetPath(destinationPath);

			var files = await GetFilePathsInDirectory(absoluteDirectoryPath, searchPattern, excludePatterns, includeSubfoldersAndFiles);
			foreach (var file in files)
			{
				string relativePath = file.AbsolutePath.Replace(absoluteDirectoryPath, "");
				var copyDestinationFilePath = fileSystem.Path.Join(absoluteDestinationPath, relativePath);
				await CopyFile(file.AbsolutePath, copyDestinationFilePath, true, overwriteFiles);
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


		public async Task<FileInfo> GetFileInfo(string fileName)
		{
			var absoluteFileName = GetPath(fileName);
			var fileInfo = fileSystem.FileInfo.New(absoluteFileName);
			return FileInfo.FromFileInfo(fileInfo, fileSystem.RootDirectory, fileSystem, typeMapping);
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

		public async Task StopListeningToFileChange(string[] fileSearchPatterns, GoalToCallInfo? goalToCall = null)
		{
			string key = $"FileWatcher_{fileSearchPatterns}_";
			if (goalToCall != null) key += goalToCall;

			var items = appContext.Where(p => p.Key.StartsWith(key));
			foreach (var item in items)
			{
				var watcher = appContext[item.Key] as IFileSystemWatcher;
				if (watcher == null) continue;

				watcher.EnableRaisingEvents = false;
				appContext.Remove(item.Key);
			}


		}

		private ConcurrentDictionary<string, Timer> timers = new ConcurrentDictionary<string, Timer>();

		[Description("debounceTime is the time in ms that is waited until action is executed to prevent multiple execution for same file. At least one listenFor variable needs to be true")]
		public async Task ListenToFileChange(List<string> fileSearchPatterns, GoalToCallInfo goalToCall,
			List<string>? excludeFiles = null,
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

								goalToCall.Parameters.AddOrReplaceDict(parameters);
								var task = pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.Path.DirectorySeparatorChar.ToString(), goalToCall);
								task.Wait();

							}, e.FullPath, debounceTime, Timeout.Infinite);
							timers.TryAdd(e.FullPath, timer);
						}

					};
				}

				watcher.EnableRaisingEvents = true;

				int counter = 0;
				while (appContext.ContainsKey($"FileWatcher_{fileSearchPattern}_{goalToCall}_{counter}")) { counter++; }
				;

				appContext.AddOrReplace($"FileWatcher_{fileSearchPattern}_{goalToCall}_{counter}", watcher);
				KeepAlive(this, $"FileWatcher [{fileSearchPattern}]");
			}
		}

		private void AddEventToTimer(object sender, FileSystemEventArgs e, long debounceTime,
			string goalToCall, List<string>? excludeFiles,
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
				timer = new Timer(async (state) =>
				{
					await WatcherCallGoal(sender, e, goalToCall, excludeFiles,
						absoluteFilePathVariableName, fileNameVariableName, changeTypeVariableName, senderVariableName);
				}, e.FullPath, debounceTime, Timeout.Infinite);
				timers.TryAdd(e.FullPath, timer);
			}
		}

		private static readonly object _lock = new object();
		private async Task WatcherCallGoal(object sender, FileSystemEventArgs e, GoalToCallInfo goalToCall, List<string>? excludeFiles,
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

				goalToCall.Parameters.AddOrReplaceDict(parameters);
				try
				{
					var task = pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.Path.DirectorySeparatorChar.ToString(), goalToCall);
					task.Wait();

					var (engine2, vars, error) = task.Result;
					if (error != null)
					{
						var errorTask = errorSystemHandlerFactory.CreateHandler().Handle(error);
						errorTask.Wait();

						var result = errorTask.Result;
						if (result.Item2 != null)
						{
							Console.WriteLine(error.ToString());
						}
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, goalStep.Text);
				}
			}
		}

		public record Pdf(List<PdfPage> Pages, int PageCount)
		{
			public string Content
			{
				get
				{
					StringBuilder sb = new();
					foreach (var page in Pages)
					{
						sb.AppendLine(page.Content);
					}
					return sb.ToString();
				}
			}
		}
		public record PdfPage(IEnumerable<string> Lines, IEnumerable Images, int PageNr, int Size)
		{
			public string Content
			{
				get
				{
					StringBuilder sb = new();
					foreach (var line in Lines)
					{
						sb.AppendLine(line);
					}
					sb.AppendLine("\n---\n");
					return sb.ToString();
				}
			}
		};

		[Description("Reads pdf file and loads into return variable. format can be md|text. imagePath can be null|base64|pathToFolder.")]
		public async Task<(Pdf, IError?)> ReadPdf(string path, string format = "md", string? imagePath = null, string? password = null)
		{
			var absolutePath = GetPath(path);
			PdfToMarkdownConverter pdfMarkdown = new PdfToMarkdownConverter(fileSystem, goal);

			var pages = pdfMarkdown.ConvertPdfToMarkdown(absolutePath, format, imagePath, password);
			var pdf = new Pdf(pages, pages.Count);

			return (pdf, null);
		}

		public async Task AddTypeMapping(string extension, string type, string? contentType = null)
		{
			var ext = extension.StartsWith('.') ? extension : $".{extension}";
			typeMapping.AddMapping(ext, type, contentType);
		}

		
	


		public void Dispose()
		{
			var fileStreams = appContext.Keys.Where(p => p.StartsWith("FileStream_"));
			foreach (var key in fileStreams)
			{
				((FileStream)appContext[key]).Dispose();
			}

			var fileWatchers = appContext.Keys.Where(p => p.StartsWith("FileWatcher_"));
			foreach (var key in fileWatchers)
			{
				var watcher = (IFileSystemWatcher)appContext[key];

				watcher?.Dispose();
			}

			foreach (var key in timers)
			{
				key.Value.Dispose();
			}
		}


	}


	public record FileInfo
	{
		public string Name { get; init; }
		public string FullName { get; init; }
		public string? DirectoryName { get; init; }
		public string Path { get; init; }
		public string Extension { get; init; }
		public string? ContentType { get; init; }
		public string? Type { get; init; }
		public long Length { get; init; }
		public bool Exists { get; init; }
		public bool IsReadOnly { get; init; }
		public DateTime Created { get; init; }
		public DateTime Updated { get; init; }
		public DateTime Accessed { get; init; }
		public FileAttributes Attributes { get; init; }

		private readonly Lazy<object?> _content;
		public object? Content => _content.Value;

		public FileInfo(
			string name,
			string fullName,
			string? directoryName,
			string path,
			string extension,
			string? contentType,
			string? type,
			long length,
			bool exists,
			bool isReadOnly,
			DateTime created,
			DateTime updated,
			DateTime accessed,
			FileAttributes attributes,
			Func<object?> contentLoader)
		{
			Name = name;
			FullName = fullName;
			DirectoryName = directoryName;
			Path = path;
			Extension = extension;
			ContentType = contentType;
			Type = type;
			Length = length;
			Exists = exists;
			IsReadOnly = isReadOnly;
			Created = created;
			Updated = updated;
			Accessed = accessed;
			Attributes = attributes;
			_content = new Lazy<object?>(contentLoader);
		}

		public static FileInfo FromFileInfo(IFileInfo fileInfo, string rootPath, IPLangFileSystem fileSystem, ITypeMapping typeMapping)
		{
			var relativePath = System.IO.Path.GetRelativePath(rootPath, fileInfo.FullName);
			var extension = fileInfo.Extension.TrimStart('.');
			var contentType = typeMapping.GetContentType(extension);
			var (type, error) = typeMapping.GetType(extension);
			if (error != null) type = "unknown";

			return new FileInfo(
				fileInfo.Name,
				fileInfo.FullName,
				fileInfo.DirectoryName,
				relativePath,
				fileInfo.Extension,
				contentType,
				type,
				fileInfo.Exists ? fileInfo.Length : 0,
				fileInfo.Exists,
				fileInfo.IsReadOnly,
				fileInfo.CreationTimeUtc,
				fileInfo.LastWriteTimeUtc,
				fileInfo.LastAccessTimeUtc,
				fileInfo.Attributes,
				() => fileInfo.Exists ? LoadContent(fileInfo.FullName, type, fileSystem) : null);
		}

		private static object? LoadContent(string fullName, string? type, IPLangFileSystem fileSystem)
		{
			return type switch
			{
				"text" => fileSystem.File.ReadAllText(fullName),
				"binary" => fileSystem.File.ReadAllBytes(fullName),
				_ => fileSystem.File.ReadAllBytes(fullName)
			};
		}
	}
}


