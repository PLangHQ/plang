using System.Dynamic;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using System.Web;
using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using PLangWindowForms;
using Scriban;
using Scriban.Syntax;
using static PLang.Executor;
using static PLang.Modules.UiModule.Program;
using Instruction = PLang.Building.Model.Instruction;

namespace PlangWindowForms;

public partial class Form1 : Form, IForm
{
    private readonly string[] args;
    private readonly ServiceContainer container;
    private readonly bool debug;


    private readonly List<FileSystemStream> fileStreams = new();
    private readonly IPLangFileSystem fileSystem;
    private readonly Executor pLang;
    private IEngine engine;

    private bool isReloading = false;


    private string? lastJsonMesage;

    private IPseudoRuntime pseudoRuntime;

    public Form1(string[] args)
    {
        AppContext.TryGetSwitch(ReservedKeywords.Debug, out debug);
        if (!debug) AppContext.TryGetSwitch(ReservedKeywords.CSharpDebug, out debug);
        AppContext.TryGetSwitch(ReservedKeywords.Debug, out debug);
        SynchronizationContext = SynchronizationContext.Current;
        this.args = args;
        container = new ServiceContainer();
        webView = new WebView2();
        container.RegisterForPLangWindowApp(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString(),
            new AskUserDialog(), new ErrorDialog(), this);

        fileSystem = container.GetInstance<IPLangFileSystem>();
        pLang = new Executor(container);

        InitializeComponent();
        InitializeWebView();
    }

    public SynchronizationContext SynchronizationContext { get; set; }


    public async Task Flush()
    {
        try
        {
            // TODO: not happy with this, it should use template engine from container
            // it should not be getting the tree from context
            var context = container.GetInstance<PLangAppContext>();
            var tree = context.GetOrDefault(ReservedKeywords.GoalTree, default(GoalTree<string>));
            if (tree == null) return;


            var stringContent = tree.PrintTree();
            var (html, error) = await CompileAndRun(stringContent);

            ShowError(error);


            context.Remove(ReservedKeywords.GoalTree);
            if (string.IsNullOrEmpty(html)) return;

            var target = engine.GetMemoryStack().Get<OutputTarget>(ReservedKeywords.OutputTarget);
            if (target != null)
                await ModifyContent(html, target, tree.GoalHash);
            else
                webView.CoreWebView2.NavigateToString(html);

            await ListenToVariables();
        }
        catch (Exception ex)
        {
            ShowErrorInDevTools(new Error(ex.Message, Exception: ex));
        }
    }

    // elementPosition: replace|replaceOuter|appendTo|afterElement|beforeElement|prependTo|insertAfter|insertBefore
    public async Task ModifyContent(string content, OutputTarget outputTarget, string id)
    {
        var escapedHtmlContent = JsonConvert.ToString(content);

        var selectors = string.Join(",", outputTarget.cssSelectors);

        var script =
            @$"plangUi.insertContent('{outputTarget.elementPosition}', '{id}', '{selectors}', {escapedHtmlContent}, {outputTarget.overwriteIfExists.ToString().ToLower()});";

        SynchronizationContext.Post(async _ =>
        {
            //var task = webView.EnsureCoreWebView2Async();
            //task.Wait();
            var str = await webView.CoreWebView2.ExecuteScriptAsync(@$"(function() {{
	try {{
		{script} 
	}} catch (e) {{ 
		return e.message;
    }}
}})();
");
            if (str != "null") ShowError(new Error(str));
        }, null);
    }

    public async Task ExecuteCode(string content)
    {
        SynchronizationContext.Post(async _ => { await webView.CoreWebView2.ExecuteScriptAsync(content); }, null);
    }


    public void SetSize(int width, int height)
    {
        Size = new Size(width, height);
    }

    public void SetIcon(string? iconPath)
    {
        if (iconPath == null) return;
        Icon = Icon.ExtractAssociatedIcon(iconPath);
    }

    public void SetTitle(string? title)
    {
        if (title == null) return;
        Text = title;
    }


    private void InitializeWebView()
    {
        webView = new WebView2();
        webView.Dock = DockStyle.Fill;
        //this.webView.Source = new Uri("about:blank");
        Controls.Add(webView);

        AllowDrop = true;
        DragEnter += Form_DragEnter;
    }

    private void Form_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Move;
        else
            e.Effect = DragDropEffects.None;
    }

    public async Task SetInitialHtmlContent()
    {
        var core = await CoreWebView2Environment.CreateAsync();

        var core2 = core.CreateCoreWebView2ControllerOptions();

        var task = webView.EnsureCoreWebView2Async();
        await task;

        if (task.Exception != null) Console.WriteLine(task.Exception);
        webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
        webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
        webView.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;
        webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
        webView.CoreWebView2.WebResourceRequested += (sender, args) => { HandleResourcesRequests(args); };

        webView.CoreWebView2.WebMessageReceived += async (sender, e) =>
        {
            await CoreWebView2_WebMessageReceived(sender, e);
        };
        if (debug) pLang.SetupDebug();

        engine = container.GetInstance<IEngine>();

        await pLang.Execute(args, ExecuteType.Runtime);
    }

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
        var outputTargetElement = "body";
        if (context.TryGetValue(ReservedKeywords.DefaultTargetElement, out var te) &&
            !string.IsNullOrWhiteSpace(te.ToString())) outputTargetElement = te.ToString() ?? "body";

        if (jObj.ContainsKey("target") && !string.IsNullOrEmpty(jObj["target"].ToString()))
            outputTargetElement = jObj["target"]?.ToString() ?? outputTargetElement;

        var parameters = new Dictionary<string, object?>();
        if (jObj.ContainsKey("args"))
            parameters = jObj["args"]?.ToObject<Dictionary<string, object?>?>() ?? new Dictionary<string, object>();
        else
            parameters = jObj.ToObject<Dictionary<string, object?>?>();

        try
        {
            (var goalName, Dictionary<string, object?>? param) = ParseUrl(jObj["GoalName"].ToString());
            parameters.Add(ReservedKeywords.OutputTarget, new OutputTarget([outputTargetElement]));
            if (param != null)
                foreach (var p in param)
                    parameters.AddOrReplace(p.Key, p.Value);

            var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
            engine.GetMemoryStack().GetMemoryStack().Clear();
            var goalResult = await pseudoRuntime.RunGoal(engine, engine.GetContext(), "", goalName, parameters);

            ShowErrorInDevTools(goalResult.error);

            lastJsonMesage = receivedMessage;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\n\n" + ex);
        }
    }

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

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        foreach (var fs in fileStreams) fs.Close();
        fileStreams.Clear();
    }

    private void CoreWebView2_ContentLoading(object? sender, CoreWebView2ContentLoadingEventArgs e)
    {
        var i = 0;
    }

    private void CoreWebView2_WebResourceResponseReceived(object? sender,
        CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        var i = 0;
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.NavigationId == 3 && e.NavigationKind == CoreWebView2NavigationKind.Reload)
        {
            if (lastJsonMesage != null)
                RenderMessage(lastJsonMesage);
            else
                SetInitialHtmlContent();
        }

        var i = 0;
    }

    private string EscapeChars(string? content)
    {
        if (content == null) return "";

        return content.Replace("\\", "\\\\") // Escape backslashes
            .Replace("'", "\\'") // Escape single quotes
            .Replace("\n", "\\n") // Escape new lines
            .Replace("\r", "\\r");
    }


    private async Task HandleEvent(JObject @event)
    {
        if (@event == null) return;

        if (@event["EventType"].Value<string>() == "OnChange")
        {
            var context = engine.GetContext();
            var goal = new Goal();
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
        templateEngine.Init(container, new Goal(), new GoalStep(), new Instruction(new object()),
            engine.GetMemoryStack(),
            container.GetInstance<ILogger>(), container.GetInstance<PLangAppContext>(),
            container.GetInstance<ITypeHelper>(), container.GetInstance<ILlmServiceFactory>(),
            container.GetInstance<ISettings>(), container.GetInstance<IAppCache>(), null);
        return await templateEngine.RenderContent(obj.ToString(), "");
    }

    private (string goalName, Dictionary<string, object?>? param) ParseUrl(string url)
    {
        var parts = url.Split('?');
        var basePath = parts[0];
        var queryString = parts.Length > 1 ? parts[1] : string.Empty;

        var queryParameters = HttpUtility.ParseQueryString(queryString);
        var parameters = new Dictionary<string, object?>();

        foreach (string key in queryParameters)
        {
            var value = queryParameters[key];
            if (value != null && Regex.IsMatch(value, "'[0-9]+'")) value = value.Replace("'", "");

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


    private void HandleResourcesRequests(CoreWebView2WebResourceRequestedEventArgs args)
    {
        var resourceType = args.ResourceContext;
        if (args.Request.Uri.StartsWith("local://ui/"))
        {
            var fileName = args.Request.Uri.Replace("local://", "");
            if (fileSystem.File.Exists(fileName))
            {
                var fs = fileSystem.File.Open(fileName, FileMode.Open, FileAccess.Read);

                fs.Position = 0;
                var mimeType = Path.GetExtension(fileName) == ".css" ? "text/css" : "application/javascript";
                args.Response =
                    webView.CoreWebView2.Environment.CreateWebResourceResponse(fs, 200, "Ok",
                        $"Content-Type: {mimeType}");

                fileStreams.Add(fs);
                return;
            }
        }


        // Allow only image, video, and audio requests
        if (resourceType != CoreWebView2WebResourceContext.Image &&
            resourceType != CoreWebView2WebResourceContext.Media)
            args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Blocked", "");
    }

    private async Task HandleErrors(string strVariables, string errorConsole)
    {
        if (string.IsNullOrEmpty(errorConsole)) return;
        if (debug) webView.CoreWebView2.OpenDevToolsWindow();

        await webView.CoreWebView2.ExecuteScriptAsync($"console.error('{EscapeChars(errorConsole)}');");
        await webView.CoreWebView2.ExecuteScriptAsync(
            $"console.info('Available Variables:\\n\\n{EscapeChars(strVariables)}');");
        AppContext.TryGetSwitch("llmerror", out var llmError);
        if (llmError)
        {
            var system =
                @"You are c# expert developer debugging an error. You job is to identify why an error occured that user provides. 

Model variable is an ExpandoObject. 
Objects in Model are Json serialized for you, it maps to an object, e.g. 
Model.address = {""street"":""Mainstreet"", zip:123} is an object that can be accessed by Model.address.street and Model.address.zip

The Razor template is generated using plang programming language. User will provide source code of ```plang. Suggest changes to the plang source file since the user is programming in plang programming language. User will not know what Razor is, so keep your facts to plang. Razor code is generated automatically by plang builder, it is not in the control of the user.  
The ""Model."" object is not visible in plang, dont mention it in your output. If error is in Model.name, then reference it as %name%

Be straight to the point, point out the most obvious reason and how to fix in plang source code. 
Be Concise";
            var question = $@"I am getting this error:{errorConsole}

These variables are available:
{strVariables}
";
            var promptMessage = new List<LlmMessage>();
            promptMessage.Add(new LlmMessage("system", system));
            promptMessage.Add(new LlmMessage("user", question));

            var llmRequest = new LlmRequest("UIError", promptMessage);
            llmRequest.llmResponseType = "text";

            await webView.CoreWebView2.ExecuteScriptAsync(
                "console.info('Analyzing error... will be back with more info in few seconds....');");
            var llmService = container.GetInstance<ILlmService>();
            var (result, queryError) = await llmService.Query<string>(llmRequest);

            await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Help:\\n\\n{EscapeChars(result)}');");
        }
    }

    public record JsVariable(string name, string goalToCall, Dictionary<string, object?>? parameters);
}