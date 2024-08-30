using LightInject;
using Microsoft.Web.WebView2.Core;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang;
using PLang.Container;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using PLangWindowForms;
using Scriban;
using Scriban.Syntax;
using System.Dynamic;
using System.IO.Abstractions;
using System.Text;
using static PLang.Executor;

namespace PlangWindowForms
{


	public partial class Form1 : Form, IForm
	{
		bool debug = false;
		ServiceContainer container;
		IEngine engine;
		IPLangFileSystem fileSystem;
		Executor pLang;
		private string[] args;
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
			pLang = new Executor(container);

			InitializeComponent();
			InitializeWebView();

		}

		private void InitializeWebView()
		{
			this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
			this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
			//this.webView.Source = new Uri("about:blank");
			this.webView.NavigationStarting += WebView_NavigationStarting;
			this.Controls.Add(this.webView);

			this.AllowDrop = true;
			this.DragEnter += new DragEventHandler(Form_DragEnter);
			SetInitialHtmlContent();
		}
		private void Form_DragEnter(object? sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Move;
			else
				e.Effect = DragDropEffects.None;
		}

		IPseudoRuntime pseudoRuntime;
		private async Task SetInitialHtmlContent()
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

			string callGoalJs = $@"

			document.addEventListener('submit', function(event) {{
				if (event.target && event.target.tagName === 'FORM') {{
					event.preventDefault();
console.log(event.target);
					let formData = new FormData(event.target);
console.log(formData);		
					let jsonData = {{}};
					formData.forEach((value, key) => {{ jsonData[key] = value }});
console.log('json:',jsonData);
var goalName = event.target.getAttribute('action');
if (goalName == null) goalName = event.target.getAttribute('hx-post');

					window.chrome.webview.postMessage({{GoalName:goalName, args:jsonData}});
				}}
            }}, true);

		 window.callGoal = function(goalName, args) {{
			console.log('goalName', goalName, 'args', args);
			window.chrome.webview.postMessage({{GoalName:goalName, args:args}});
		}}";
			await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(callGoalJs);


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


		public async Task Flush()
		{
			// TODO: not happy with this, it should use template engine from container
			// it should not be getting the tree from context
			var context = container.GetInstance<PLangAppContext>();
			var tree = context.GetOrDefault(ReservedKeywords.GoalTree, default(GoalTree<string>));
			if (tree == null) return;


			var stringContent = tree.PrintTree();
			var html = await CompileAndRun(stringContent);

			context.Remove(ReservedKeywords.GoalTree);
			if (html == null) return;

			var target = engine.GetMemoryStack().Get<string>("!WebViewTarget");
			if (target != null)
			{
				await ModifyContent(target, html);
			}
			else
			{
				webView.CoreWebView2.NavigateToString(html);
			}
		}



		private async Task<string> CompileAndRun(object? obj)
		{
			var expandoObject = new ExpandoObject() as IDictionary<string, object?>;
			var templateContext = new TemplateContext();
			foreach (var kvp in engine.GetMemoryStack().GetMemoryStack())
			{
				expandoObject.Add(kvp.Key, kvp.Value.Value);

				var sv = ScriptVariable.Create(kvp.Key, ScriptVariableScope.Global);
				templateContext.SetValue(sv, kvp.Value.Value);
			}

			var parsed = Template.Parse(obj.ToString());
			var result = await parsed.RenderAsync(templateContext);

			return result;
		}


		private async Task CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			var receivedMessage = e.WebMessageAsJson;
			if (receivedMessage == "\"{}\"") return;

			var dynamic = JsonConvert.DeserializeObject<dynamic>(receivedMessage);

			var parameters = new Dictionary<string, object>();
			if (dynamic is string)
			{
				int i = 0;
				return;
			}
			if (dynamic is JObject jObj)
			{
				if (jObj.ContainsKey("args"))
				{
					parameters = jObj["args"].ToObject<Dictionary<string, object>>();
				}
				else
				{
					parameters = jObj.ToObject<Dictionary<string, object>>();
				}
				if (parameters == null) parameters = new();
			}
			else if (dynamic is JArray jArray)
			{

				parameters = jArray.ToObject<Dictionary<string, object>>();

				if (parameters == null) parameters = new();
			}
			try
			{

				parameters.Add("!WebViewTarget", ".uk-container");
				var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
				await pseudoRuntime.RunGoal(engine, engine.GetContext(), "", dynamic.GoalName.ToString(), parameters);

			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + "\n\n" + ex.ToString());
			}
		}

		// innerHTML - outerHTML - beforebegin - afterbegin - beforeend - afterend
		public async Task ModifyContent(string cssSelector, string? content, string swapping = "innerHTML")
		{
			string escapedHtmlContent = JsonConvert.ToString(content);
			string action = $"targetElement.innerHTML = {escapedHtmlContent};";
			if (swapping == "outerHTML")
			{
				action = $"targetElement.outerHTML = {escapedHtmlContent};";
			}
			if (swapping == "beforebegin")
			{
				action = $"targetElement.outerHTML = {escapedHtmlContent} + targetElement.outerHTML;";
			}
			if (swapping == "afterbegin")
			{
				action = $"targetElement.innerHTML = {escapedHtmlContent} + targetElement.innerHTML;";
			}
			if (swapping == "beforeend")
			{
				action = $"targetElement.innerHTML += {escapedHtmlContent};";
			}
			if (swapping == "afterend")
			{
				action = $"targetElement.outerHTML = targetElement.outerHTML + {escapedHtmlContent};";
			}

			string script = $@"
        var targetElement = document.querySelector('{cssSelector}');
        if (targetElement) {{
            {action}
        }}
    ";
			SynchronizationContext.Post(async _ =>
			{
				//var task = webView.EnsureCoreWebView2Async();
				//task.Wait();
				await webView.CoreWebView2.ExecuteScriptAsync(script);
			}, null);

		}

		public async Task ExecuteCode(string content)
		{
			SynchronizationContext.Post(async _ =>
			{
				await webView.CoreWebView2.ExecuteScriptAsync(content);
			}, null);

		}

		bool initialLoad = false;
		private void RenderContent(string html)
		{
			var outputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			var outputStream = outputStreamFactory.CreateHandler();

			var stream = outputStream.Stream;
			var errorStream = outputStream.ErrorStream;
			if (html == "Loading...")
			{
				if (initialLoad) return;
				initialLoad = true;
			}

			(string strVariables, string errorConsole) = ExtractErrorMessages(errorStream);

			webView.CoreWebView2.NavigationCompleted += async (object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) =>
			{
				await HandleErrors(strVariables, errorConsole);
			};
			webView.CoreWebView2.OpenDevToolsWindow();
			webView.CoreWebView2.ExecuteScriptAsync($"console.error('{EscapeChars(html)}');");

			outputStream.Stream.Position = 0;
		}

		private (string strVariables, string errorConsole) ExtractErrorMessages(Stream errorStream)
		{
			string strVariables = "";
			string errorConsole = "";
			if (errorStream != null)
			{
				errorStream.Position = 0;

				using (StreamReader reader = new StreamReader(errorStream, Encoding.UTF8, leaveOpen: true))
				{
					errorConsole = reader.ReadToEnd();
				}


				if (!string.IsNullOrEmpty(errorConsole))
				{
					var variables = engine.GetMemoryStack().GetMemoryStack();

					foreach (var variable in variables)
					{
						strVariables += "\\t" + variable.Key + ":" + JsonConvert.SerializeObject(variable.Value.Value) + "\\n";
					}
				}

			}
			return (strVariables, errorConsole);
		}

		private void HandleResourcesRequests(CoreWebView2WebResourceRequestedEventArgs args)
		{
			var resourceType = args.ResourceContext;
			if (args.Request.Uri.StartsWith("local://ui/"))
			{
				string fileName = args.Request.Uri.Replace("local://", "");
				if (fileSystem.File.Exists(fileName))
				{
					var fs = fileSystem.File.Open(fileName, FileMode.Open, FileAccess.Read);

					fs.Position = 0;
					string mimeType = (Path.GetExtension(fileName) == ".css") ? "text/css" : "application/javascript";
					args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(fs, 200, "Ok", $"Content-Type: {mimeType}");

					fileStreams.Add(fs);
					return;

				}
			}


			// Allow only image, video, and audio requests
			if (resourceType != CoreWebView2WebResourceContext.Image &&
			resourceType != CoreWebView2WebResourceContext.Media)
			{
				args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Blocked", "");
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

		private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
		{
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


		private void WebView_NavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
		{
			if (e.Uri.StartsWith("app://"))
			{
				e.Cancel = true;

				if (e.Uri == "app://buttonclicked")
				{
					MessageBox.Show("Button Clicked", "You clicked the button in the WebView2!", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
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


	}
}