using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Web.WebView2.Core;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang;
using PLang.Container;
using PLang.Errors;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Utils;
using PLangWindowForms;
using Scriban;
using Scriban.Syntax;
using System.Dynamic;
using System.IO.Abstractions;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using static PLang.Executor;
using static PLang.Modules.UiModule.Program;
using System.ComponentModel;
using System.Net.Http;
using System;

namespace PlangWindowForms
{


	public partial class Form1 : Form, IForm
	{
		bool debug = false;
		ServiceContainer container;
		IEngine engine;
		IPLangFileSystem fileSystem;
		IOutputStreamFactory outputStreamFactory;
		Executor pLang;
		private string[] args;
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public SynchronizationContext SynchronizationContext { get; set; }
		public Form1(string[] args)
		{

			AppContext.TryGetSwitch(ReservedKeywords.Debug, out debug);
			if (!debug)
			{
				AppContext.TryGetSwitch(ReservedKeywords.CSharpDebug, out debug);
			}
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out debug);
			SynchronizationContext = SynchronizationContext.Current;
			this.args = args;
			container = new ServiceContainer();
			this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
			container.RegisterForPLangWindowApp(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString(),
				new AskUserDialog(), new ErrorDialog(), this);

			fileSystem = container.GetInstance<IPLangFileSystem>();
			outputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			pLang = new Executor(container);

			InitializeComponent();
			InitializeWebView();

		}


		private void InitializeWebView()
		{
			this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
			this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
			//this.webView.Source = new Uri("about:blank");
			this.Controls.Add(this.webView);

			this.AllowDrop = true;
			this.DragEnter += new DragEventHandler(Form_DragEnter);
			
		}
		private void Form_DragEnter(object? sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Move;
			else
				e.Effect = DragDropEffects.None;
		}

		IPseudoRuntime pseudoRuntime;
		public async Task SetInitialHtmlContent()
		{
			var core = await CoreWebView2Environment.CreateAsync();

			var core2 = core.CreateCoreWebView2ControllerOptions();

			var task = webView.EnsureCoreWebView2Async();
			await task;

			if (task.Exception != null)
			{
				Console.WriteLine(task.Exception);
			}
			webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
			webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
			webView.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;
			webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
			webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
			webView.CoreWebView2.WebResourceRequested += (sender, args) =>
			{
				HandleResourcesRequests(args);
			};

			this.webView.CoreWebView2.WebMessageReceived += async (object? sender, CoreWebView2WebMessageReceivedEventArgs e) =>
			{
				await CoreWebView2_WebMessageReceived(sender, e);
			};
			if (debug)
			{
				pLang.SetupDebug();
			}

			engine = container.GetInstance<IEngine>();

			await pLang.Execute(args, ExecuteType.Runtime);
		}


		public async Task Flush(string str)
		{
			try
			{
				// TODO: not happy with this, it should use template engine from container
				// it should not be getting the tree from context
				var context = container.GetInstance<PLangAppContext>();

				

				webView.CoreWebView2.NavigateToString(str);
				
				/*
				var stringContent = tree.PrintTree();
				(var html, var error) = await CompileAndRun(stringContent);

				ShowError(error);


				context.Remove(ReservedKeywords.GoalTree);
				if (string.IsNullOrEmpty(html)) return;

				var target = engine.GetMemoryStack().Get<OutputTarget>(ReservedKeywords.OutputTarget);
				if (target != null)
				{
					await ModifyContent(html, target, tree.GoalHash);
				}
				else
				{
					webView.CoreWebView2.NavigateToString(html);
				}

				await ListenToVariables();
				*/
			} catch(Exception ex)
			{
				ShowErrorInDevTools(new Error(ex.Message, Exception: ex));
			}
		}


		private string? lastJsonMesage = null;

		private async Task CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			var receivedMessage = e.WebMessageAsJson;
			await RenderMessage(receivedMessage);
		}

		private async Task RenderMessage(string receivedMessage)
		{
			if (receivedMessage == "\"{}\"") return;

			var jObj = JObject.Parse(receivedMessage);
			if (jObj.ContainsKey("EventType"))
			{
				await HandleEvent(jObj);
				return;
			}

			var context = engine.GetContext();
			string outputTargetElement = "body";
			if (context.TryGetValue(ReservedKeywords.DefaultTargetElement, out object? te) && !string.IsNullOrWhiteSpace(te.ToString()))
			{
				outputTargetElement = te.ToString() ?? "body";
			}

			if (jObj.ContainsKey("target") && !string.IsNullOrEmpty(jObj["target"].ToString()))
			{
				outputTargetElement = jObj["target"]?.ToString() ?? outputTargetElement;
			}

			var parameters = new Dictionary<string, object?>();
			if (jObj.ContainsKey("args"))
			{
				parameters = jObj["args"]?.ToObject<Dictionary<string, object?>?>() ?? new();
			}
			else
			{
				parameters = jObj.ToObject<Dictionary<string, object?>?>();
			}

			try
			{
				(string goalName, Dictionary<string, object?>? param) = ParseUrl(jObj["GoalName"].ToString());
				parameters.Add(ReservedKeywords.OutputTarget, new OutputTarget([outputTargetElement]));
				if (param != null)
				{
					foreach (var p in param)
					{
						parameters.AddOrReplace(p.Key, p.Value);
					}
				}
				var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
				engine.GetMemoryStack().GetMemoryStack().Clear();
				var goalResult = await pseudoRuntime.RunGoal(engine, engine.GetContext(), "", goalName, parameters);

				ShowErrorInDevTools(goalResult.error);

				lastJsonMesage = receivedMessage;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + "\n\n" + ex.ToString());
			}
		}

		public record JsVariable(string name, string goalToCall, Dictionary<string, object?>? parameters);
		public async Task ListenToVariables()
		{
			var variables = engine.GetMemoryStack().GetVariablesWithEvent(VariableEventType.OnChange);
			List<JsVariable> jsVars = new();
			foreach (var variable in variables)
			{
				var @event = variable.Events.FirstOrDefault(p => p.EventType == VariableEventType.OnChange);
				if (@event == null) continue;

				jsVars.Add(new JsVariable(variable.Name, @event.goalName, @event.Parameters));
			}
			var jsVarsJson = JsonConvert.ToString(JsonConvert.SerializeObject(jsVars));

			await ExecuteCode($@"plangUi.setVariablesToMonitor({jsVarsJson});");

		}

		// elementPosition: replace|replaceOuter|appendTo|afterElement|beforeElement|prependTo|insertAfter|insertBefore
		public async Task ModifyContent(string content, OutputTarget outputTarget, string id)
		{
			string escapedHtmlContent = JsonConvert.ToString(content);
			
			string selectors = string.Join(",", outputTarget.cssSelectors);

			string script = @$"plangUi.insertContent('{outputTarget.elementPosition}', '{id}', '{selectors}', {escapedHtmlContent}, {outputTarget.overwriteIfExists.ToString().ToLower()});";

			SynchronizationContext.Post(async _ =>
			{
				//var task = webView.EnsureCoreWebView2Async();
				//task.Wait();
				string str = await webView.CoreWebView2.ExecuteScriptAsync(@$"(function() {{
	try {{
		{script} 
	}} catch (e) {{ 
		return e.message;
    }}
}})();
");
				if (str != "null")
				{
					ShowError(new Error(str));
				}
			}, null);

			
		}

		public async Task ExecuteCode(string content)
		{
			SynchronizationContext.Post(async _ =>
			{
				await webView.CoreWebView2.ExecuteScriptAsync(content);
			}, null);

		}



		List<FileSystemStream> fileStreams = new List<FileSystemStream>();

		private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
		{
			foreach (var fs in fileStreams)
			{
				fs.Close();
			}
			fileStreams.Clear();
		}

		private void CoreWebView2_ContentLoading(object? sender, CoreWebView2ContentLoadingEventArgs e)
		{
			int i = 0;
			
		}

		private void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
		{
			int i = 0;
		}
		bool isReloading = false;
		private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
		{
			if (e.NavigationId == 3 && e.NavigationKind == CoreWebView2NavigationKind.Reload)
			{
				if (lastJsonMesage != null)
				{
					RenderMessage(lastJsonMesage);
				}
				else
				{
					SetInitialHtmlContent();
					
				}
			} else if (e.Uri.StartsWith("plang:"))
			{
				var parsedUrl = ParseUrl(e.Uri);
				var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
				//engine.GetMemoryStack().GetMemoryStack().Clear();
				//goalName = args.Request.Uri.ToString().Replace("plang:", "", StringComparison.OrdinalIgnoreCase);
				Task.Run(() =>
				{
					var task = pseudoRuntime.RunGoal(engine, engine.GetContext(), "", parsedUrl.goalName, parsedUrl.param);
					task.Wait();
					var goalResult = task.Result;
					int i = 0;
				});

			}
			int i = 0;
		}

		private string EscapeChars(string? content)
		{
			if (content == null) return "";

			return content.Replace("\\", "\\\\") // Escape backslashes
								.Replace("'", "\\'")   // Escape single quotes
								.Replace("\n", "\\n")  // Escape new lines
								.Replace("\r", "\\r");
		}


		public void SetSize(int width, int height)
		{
			this.Size = new Size(width, height);

		}

		public void SetIcon(string? iconPath)
		{
			if (iconPath == null) return;
			this.Icon = Icon.ExtractAssociatedIcon(iconPath);
		}

		public void SetTitle(string? title)
		{
			if (title == null) return;
			this.Text = title;

		}





		private async Task HandleEvent(JObject @event)
		{
			if (@event == null) return;

			if (@event["EventType"].Value<string>() == "OnChange")
			{
				var context = engine.GetContext();
				var goal = new PLang.Building.Model.Goal();
				context.AddOrReplace(ReservedKeywords.Goal, goal);

				var variable = @event["Variable"];
				var memoryStack = engine.GetMemoryStack();
				memoryStack.Put(variable["name"].Value<string>(), variable["value"].Value<object?>());

			}
		}


		private async Task<(string?, IError?)> CompileAndRun(object? obj)
		{
			var expandoObject = new ExpandoObject() as IDictionary<string, object?>;
			var templateContext = new TemplateContext();
			foreach (var kvp in engine.GetMemoryStack().GetMemoryStack())
			{
				expandoObject.Add(kvp.Key, kvp.Value.Value);

				var sv = ScriptVariable.Create(kvp.Key, ScriptVariableScope.Global);
				templateContext.SetValue(sv, kvp.Value.Value);
			}

			var templateEngine = container.GetInstance<PLang.Modules.TemplateEngineModule.Program>();
			templateEngine.Init(container, new PLang.Building.Model.Goal(), new PLang.Building.Model.GoalStep(), new PLang.Building.Model.Instruction(new()), engine.GetMemoryStack(),
				container.GetInstance<ILogger>(), container.GetInstance<PLangAppContext>(), container.GetInstance<ITypeHelper>(), container.GetInstance<ILlmServiceFactory>(),
				container.GetInstance<ISettings>(), container.GetInstance<IAppCache>(), null);
			return await templateEngine.RenderContent(obj.ToString(), "");
			
		}

		private (string goalName, Dictionary<string, object?>? param) ParseUrl(string url)
		{
			var parts = url.Split('?');
			string basePath = parts[0].Replace("plang:", "", StringComparison.OrdinalIgnoreCase);
			string queryString = parts.Length > 1 ? parts[1] : string.Empty;

			var queryParameters = HttpUtility.ParseQueryString(queryString);
			var parameters = new Dictionary<string, object?>();

			foreach (string key in queryParameters)
			{
				string? value = queryParameters[key];
				if (value != null && Regex.IsMatch(value, "'[0-9]+'"))
				{
					value = value.Replace("'", "");
				}

				parameters[key] = value;
			}
			return (basePath, parameters);
		}

		private void ShowErrorInDevTools(IError? error)
		{
			if (error == null) return;


			webView.CoreWebView2.OpenDevToolsWindow();
			webView.CoreWebView2.ExecuteScriptAsync($"console.error('{EscapeChars(error.ToString())}');");
		}

		private void ShowError(IError? error)
		{
			if (error == null) return;

			var errorDialog = new ErrorDialog();
			errorDialog.ShowDialog(error, "Error");

			//webView.CoreWebView2.OpenDevToolsWindow();
			//webView.CoreWebView2.ExecuteScriptAsync($"console.error('{EscapeChars(html)}');");
		}

		public static Stream ConvertStringToStream(string input, Encoding? encoding = null)
		{
			encoding ??= Encoding.UTF8; // Default to UTF-8 if no encoding is provided

			var memoryStream = new MemoryStream();
			using (var writer = new StreamWriter(memoryStream, encoding, leaveOpen: true))
			{
				writer.Write(input);
				writer.Flush();
			}
			memoryStream.Position = 0;
			return memoryStream;
		}
		private void HandleResourcesRequests(CoreWebView2WebResourceRequestedEventArgs args)
		{
			var resourceType = args.ResourceContext;
			bool isMedia = resourceType == CoreWebView2WebResourceContext.Image || resourceType == CoreWebView2WebResourceContext.Media;
			bool isLocalFileRequest = args.Request.Uri.StartsWith("plang:");
			string? mimeType = MimeTypeHelper.GetWebMimeType(args.Request.Uri.Replace("plang:", ""));
			if (mimeType != null)
			{
				//isMedia = true;
			}
			if (!isMedia && args.Request.Uri.StartsWith("plang:"))
			{
				(string goalName, Dictionary<string, object?>? param) = ParseUrl(args.Request.Uri);
				var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
				//engine.GetMemoryStack().GetMemoryStack().Clear();
				//goalName = args.Request.Uri.ToString().Replace("plang:", "", StringComparison.OrdinalIgnoreCase);
				var task =  pseudoRuntime.RunGoal(engine, engine.GetContext(), "", goalName, param);
				task.Wait();
				var goalResult = task.Result;

				var stream = ConvertStringToStream(goalResult.output.Data.ToString());
				args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(stream, 200, "Ok", $"Content-Type: text/html");
			} else if (args.Request.Uri.StartsWith("local:"))
			{
				string fileName = args.Request.Uri.Replace("local:", "");

				if (fileSystem.File.Exists(fileName))
				{
					var fs = fileSystem.File.Open(fileName, FileMode.Open, FileAccess.Read);

					fs.Position = 0;
					mimeType = (Path.GetExtension(fileName) == ".css") ? "text/css" : "application/javascript";
					args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(fs, 200, "Ok", $"Content-Type: {mimeType}");

					fileStreams.Add(fs);
					return;

				}
			}


			// Allow only image, video, and audio requests
			if (isMedia)
			{

				if (args.Request.Uri.StartsWith("http://") || args.Request.Uri.StartsWith("https://"))
				{
					return;
				}
					var url = ParseUrl(args.Request.Uri);
				var absolutePath = Path.Join(fileSystem.RootDirectory, url.goalName);
				if (!fileSystem.File.Exists(absolutePath))
				{
					return;
				}
				try
				{
					using (var fs = fileSystem.FileStream.New(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
	bufferSize: 4096,
	FileOptions.SequentialScan))
					{

						args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(fs, 404, "Blocked", "");
					}
				} catch (Exception ex)
				{
					int i = 0;
				}
			}
		}

		private async Task HandleErrors(string strVariables, string errorConsole)
		{
			if (string.IsNullOrEmpty(errorConsole)) return;
			if (debug)
			{
				webView.CoreWebView2.OpenDevToolsWindow();
			}

			await webView.CoreWebView2.ExecuteScriptAsync($"console.error('{EscapeChars(errorConsole)}');");
			await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Available Variables:\\n\\n{EscapeChars(strVariables)}');");
			AppContext.TryGetSwitch("llmerror", out bool llmError);
			if (llmError)
			{
				string system = @"You are c# expert developer debugging an error. You job is to identify why an error occured that user provides. 

Model variable is an ExpandoObject. 
Objects in Model are Json serialized for you, it maps to an object, e.g. 
Model.address = {""street"":""Mainstreet"", zip:123} is an object that can be accessed by Model.address.street and Model.address.zip

The Razor template is generated using plang programming language. User will provide source code of ```plang. Suggest changes to the plang source file since the user is programming in plang programming language. User will not know what Razor is, so keep your facts to plang. Razor code is generated automatically by plang builder, it is not in the control of the user.  
The ""Model."" object is not visible in plang, dont mention it in your output. If error is in Model.name, then reference it as %name%

Be straight to the point, point out the most obvious reason and how to fix in plang source code. 
Be Concise";
				string question = $@"I am getting this error:{errorConsole}

These variables are available:
{strVariables}
";
				var promptMessage = new List<LlmMessage>();
				promptMessage.Add(new LlmMessage("system", system));
				promptMessage.Add(new LlmMessage("user", question));

				var llmRequest = new LlmRequest("UIError", promptMessage);
				llmRequest.llmResponseType = "text";

				await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Analyzing error... will be back with more info in few seconds....');");
				var llmService = container.GetInstance<ILlmService>();
				(var result, var queryError) = await llmService.Query<string>(llmRequest);

				await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Help:\\n\\n{EscapeChars(result)}');");
			}
		}
	}
}