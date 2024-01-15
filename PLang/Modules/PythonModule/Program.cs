using Microsoft.Extensions.Logging;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities.Zlib;
using PLang.Attributes;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Utils;
using Python.Runtime;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PLang.Modules.PythonModule
{
    [Description("Runs python scripts. Parameters can be passed to the python process")]
	public class Program : BaseProgram
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IOutputStream outputStream;
		private readonly MemoryStack memoryStack;
		private readonly Signature signature;

		public Program(IPLangFileSystem fileSystem, ILogger logger, ISettings settings, IOutputStream outputStream, MemoryStack memoryStack, Signature signature) : base()
		{
			this.fileSystem = fileSystem;
			this.logger = logger;
			this.settings = settings;
			this.outputStream = outputStream;
			this.memoryStack = memoryStack;
			this.signature = signature;
		}

		[Description("Run a python script. parameterNames should be equal length as parameterValues. Parameter example name=%name%. variablesToExtractFromPythonScript are keys in the format [a-zA-Z0-9_\\.]+ that the user want to write to")]
		public async Task<Dictionary<string, object>> RunPythonScript(string fileName = "main.py",
			string[] parameterValues = null, string[] parameterNames = null,
			[HandlesVariable] string[] variablesToExtractFromPythonScript = null,
			bool useNamedArguments = false, bool useTerminal = false, string pythonPath = null,
			string stdOutVariableName = null, string stdErrorVariableName = null)
		{

			var result = new Dictionary<string, object>();
			var program = new TerminalModule.Program(logger, settings, outputStream, memoryStack);

			if (fileSystem.File.Exists("requirements.txt"))
			{
				result = await program.RunTerminal("pip install -r requirements.txt");
				foreach (var item in result)
				{
					result.AddOrReplace(item.Key, item.Value);
				}
			}


			try
			{

				if (!string.IsNullOrEmpty(pythonPath))
				{
					Python.Runtime.Runtime.PythonDLL = pythonPath;
				}
				else
				{
					// Set the Python DLL based on the OS
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						var localPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
						var pythonRootDir = Path.Join(localPath, "\\Programs\\Python\\");
						if (fileSystem.Directory.Exists(pythonRootDir))
						{
							var pythonDirs = fileSystem.Directory.GetDirectories(Path.Join(pythonRootDir)).ToList().OrderBy(p => p).ToList();
							if (pythonDirs.Count > 0)
							{
								Python.Runtime.Runtime.PythonDLL = Path.Join(pythonDirs[0], Path.GetFileName(pythonDirs[0]).ToLower() + ".dll");
							}

						}
					}
					else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					{
						//need to figure out what the path is on Linux
						Python.Runtime.Runtime.PythonDLL = "libpython3.8.so";
					}
					else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					{
						//need to figure out what the path is on MacOs
						Python.Runtime.Runtime.PythonDLL = "libpython3.8.dylib";
					}
					else
					{
						logger.LogWarning("Unsupported OS platform.");
					}
				}


				PythonEngine.Initialize();

				string capturedStdout = "", capturedStderr = "";

				using (Py.GIL())
				{
					dynamic sys = Py.Import("sys");
					dynamic io = Py.Import("io");

					sys.stdout = io.StringIO();
					sys.stderr = io.StringIO();

					try
					{
						dynamic pyModule = Py.Import("__main__");

						pyModule.plangSign = new Func<string, string, string, string, PyObject>((input, method, url, contract) =>
						{
							var result = signature.Sign(input, method, url, contract);
							PyDict pyResult = new PyDict();
							foreach (var kv in result)
							{
								pyResult[new PyString(kv.Key)] = new PyString(kv.Value);
							}
							return pyResult;
						});

						/*
						 * TODO: Not happy with this solution. It is injecting code into the python script, that is bad design :(
						 */

						List<PyObject> args = new List<PyObject>();
						args.Add(new PyString(fileName));
						for (int i = 0; parameterValues != null && i < parameterValues.Length; i++)
						{
							if (parameterNames != null || useNamedArguments)
							{
								string paramName = (parameterNames[i].StartsWith("--")) ? parameterNames[i] : "--" + parameterNames[i];
								args.Add(new PyString(paramName));
							}
							args.Add(new PyString(parameterValues[i]));
						}

						sys.argv = new PyList(args.ToArray());

						string code = fileSystem.File.ReadAllText(fileName);
						variablesToExtractFromPythonScript = variablesToExtractFromPythonScript.Select(p => p.Replace("%", "")).ToArray();
						string pythonList = "['" + string.Join("', '", variablesToExtractFromPythonScript) + "']";

						string appendedCode = $"\n\nimport __main__\n";
						appendedCode += $"__main__.plang_export_variables_dict = {{k: v for k, v in globals().items() if k in {pythonList}}}";

						code += appendedCode;
						PythonEngine.Exec(code);

						capturedStdout = sys.stdout.getvalue().ToString();
						capturedStderr = sys.stderr.getvalue().ToString();

						if (variablesToExtractFromPythonScript != null)
						{
							dynamic variablesDict = Py.Import("__main__").GetAttr("plang_export_variables_dict");

							dynamic iterItems = variablesDict.items();
							foreach (PyObject item in iterItems)
							{
								var key = item[0].ToString();

								if (variablesToExtractFromPythonScript.FirstOrDefault(p => p == key) != null)
								{
									var value = ConvertValue(item[1]);
									result.AddOrReplace(key, value);
								}
							}

						}
					}
					catch (PythonException ex2)
					{

						capturedStdout = sys.stdout.getvalue().ToString();
						capturedStderr = sys.stderr.getvalue().ToString();

						if (stdOutVariableName == null)
						{
							logger.LogWarning(capturedStdout);
						}
						if (capturedStderr == null)
						{
							logger.LogWarning(capturedStderr);
						}

						var pe = new Exception(capturedStderr + " " + capturedStdout + "\n\n" + ex2.StackTrace, ex2);
						throw pe;
					}
					finally
					{
						if (stdOutVariableName != null)
						{
							memoryStack.Put(stdOutVariableName, capturedStdout);
						}
						if (stdErrorVariableName != null)
						{
							memoryStack.Put(stdErrorVariableName, capturedStderr);
						}

						sys.stdout = sys.__stdout__;
						sys.stderr = sys.__stderr__;
					}
				}



			}
			catch (System.TypeInitializationException ex1)
			{
				useTerminal = true;
			}

			catch (Exception ex)
			{
				throw;
			}
			finally
			{
				// Shutdown the Python engine when done
				PythonEngine.Shutdown();
			}



			if (useTerminal)
			{

				if (pythonPath == null)
				{

					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						pythonPath = "python.exe";
					}
					else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					{
						pythonPath = "python3";
					}
					else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					{
						pythonPath = "python3";
					}
					else
					{
						logger.LogWarning("Unsupported OS platform.");
					}
					;

				}
				var parameterList = new List<string>();
				parameterList.Add(fileName);
				for (int i = 0; i < parameterNames.Length; i++)
				{
					parameterList.Add($"--{parameterNames[i]}={parameterValues[i]} ");
				}
				string dataOutputVariable = "";
				string errorDebugInfoOutputVariable = "";

				var action = instruction.Action as JArray;
				if (action != null)
				{
					var outputs = action.SelectTokens("$.[0].Outputs");
					var jobj = outputs.FirstOrDefault();
					if (outputs != null)
					{
						foreach (var output in jobj)
						{
							if ("dataOutputVariable".ToLower().Contains(output.FirstOrDefault().ToString().Replace("%", "").ToLower()))
							{
								dataOutputVariable = output.FirstOrDefault().ToString();
							}
							if ("errorDebugInfoOutputVariable".ToLower().Contains(output.FirstOrDefault().ToString().Replace("%", "").ToLower()))
							{
								errorDebugInfoOutputVariable = output.FirstOrDefault().ToString();
							}
						}
					}
				}

				result = await program.RunTerminal(pythonPath, parameterList, dataOutputVariable, errorDebugInfoOutputVariable);
			}
			return result;

		}

		private object ConvertValue(PyObject pyObject)
		{
			dynamic pyType = pyObject.GetPythonType();
			string typeName = pyType.__name__.ToString();

			switch (typeName)
			{
				case "int":
					return pyObject.As<int>();
				case "float":
					return pyObject.As<double>();
				case "str":
					return pyObject.As<string>();
				case "list":
					return pyObject.As<List<object>>();
				case "dict":
					return pyObject.As<Dictionary<object, object>>();
				default:
					return pyObject.As<object>().ToString();
			}
		}
	}
}

