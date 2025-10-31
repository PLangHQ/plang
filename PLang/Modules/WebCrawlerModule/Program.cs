using AngleSharp.Dom;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NBitcoin;
using NBitcoin.Secp256k1;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Runtime;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PLang.Modules.WebCrawlerModule
{

	public class BrowserModuleData
	{
		public BrowserInstance? BrowserInstance { get; set; }
		public string Url { get; set; }
		public string CssSelector { get; set; }
		public Microsoft.Playwright.IResponse? Response { get; internal set; }
		public Microsoft.Playwright.IRequest Request { get; internal set; }
		public IConsoleMessage Console { get; internal set; }
		public List<IDialog>? Dialogs { get; internal set; }
		public IFileChooser FileChooser { get; internal set; }
		public IDownload Download { get; internal set; }
	}

	[Description("Run a browser instance, browse a website, input values and click on html elements, sendkeys, wait for browser and extract content")]
	public class Program : BaseProgram, IAsyncConstructor
	{

		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private readonly IEngine engine;
		private readonly IPseudoRuntime runtime;
		private readonly ProgramFactory programFactory;
		private BrowserModuleData data;

		private object locker = new object();
		public Program(IPLangFileSystem fileSystem, ILogger logger, IEngine engine, IPseudoRuntime runtime, ProgramFactory programFactory) : base()
		{
			this.fileSystem = fileSystem;
			this.logger = logger;
			this.engine = engine;
			this.runtime = runtime;
			this.programFactory = programFactory;			
		}

		public async Task<IError?> AsyncConstructor()
		{
			data = context.GetModuleData<BrowserModuleData>();
			return null;
		}

		public async Task<BrowserInstance> GetBrowserInstance(string browserType = "Chrome", bool headless = false, string profileName = "",
			bool kioskMode = false, Dictionary<string, object>? argumentOptions = null, int? timoutInSeconds = 30, bool hideTestingMode = false,
			GoalToCallInfo? onRequest = null, GoalToCallInfo? onResponse = null)
		{
			return data.BrowserInstance ?? await StartBrowser(browserType, headless, profileName, kioskMode, argumentOptions, timoutInSeconds,
				hideTestingMode, onRequest, onResponse);
		}

		private string GetChromeUserDataDir()
		{
			string userDataDir = "";
			string userName = Environment.UserName;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				userDataDir = $@"C:\Users\{userName}\AppData\Local\Google\Chrome\User Data";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				userDataDir = $@"/Users/{userName}/Library/Application Support/Google/Chrome";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				userDataDir = $@"/home/{userName}/.config/google-chrome";
			}

			return userDataDir;
		}

		[Description("browserType=Chrome|Edge|Firefox|Safari. hideTestingMode tries to disguise that it is a bot. when user want to use the default profile, set profileName=\"default\"")]
		public async Task<BrowserInstance> StartBrowser(string browserType = "Chrome", bool headless = false, string profileName = "",
			bool kioskMode = false, Dictionary<string, object>? argumentOptions = null, int? timoutInSeconds = 30, bool hideTestingMode = false,
			GoalToCallInfo? onRequest = null, GoalToCallInfo? onResponse = null)
		{
			var browserInstance = data.BrowserInstance;
			if (browserInstance != null)
			{
				return browserInstance;
			}

			var playwright = await Playwright.CreateAsync();
			var browser = await GetBrowserType(playwright, browserType, headless, profileName, kioskMode, argumentOptions, hideTestingMode);

			browser.SetDefaultTimeout((timoutInSeconds ?? 30) * 1000);

			browserInstance = new BrowserInstance(playwright, browser);
			data.BrowserInstance = browserInstance;

			await browser.RouteAsync("*/**", async route =>
			{
				var routeAsyncUrl = browserInstance.RouteAsyncByUrl;
				if (routeAsyncUrl == null)
				{
					await route.ContinueAsync();
				}
				else
				{
					var request = route.Request;
					RouteAsync? routeAsync = null;
					foreach (var routeUrl in routeAsyncUrl)
					{
						try
						{
							if (routeUrl.Key == "**/*")
							{
								routeAsync = routeUrl.Value;
								break;
							}
							else if (Regex.IsMatch(request.Url, routeUrl.Key, RegexOptions.IgnoreCase))
							{
								routeAsync = routeUrl.Value;
								break;
							}
						}
						catch (Exception ex)
						{
							logger.LogError(ex, $"Error parsing regex for {request.Url}");
						}
					}


					if (routeAsync == null)
					{
						await route.ContinueAsync();
					}
					else
					{
						var filteredHeaders = route.Request.Headers;

						filteredHeaders = filteredHeaders
							.Where(kv => !routeAsync.HeadersToRemove
								.Any(h => kv.Key.Equals(h, StringComparison.OrdinalIgnoreCase)))
							.ToDictionary(kv => kv.Key, kv => kv.Value);

						var options = new RouteContinueOptions();
						options.Headers = filteredHeaders;

						//await route.FetchAsync(options);
						await route.ContinueAsync(options);
					}
				}


			});

			var callGoal = programFactory.GetProgram<CallGoalModule.Program>(goalStep);
			if (onRequest != null)
			{
				browser.Request += async (sender, e) =>
				{
					onRequest.Parameters.Add("!sender", sender);
					onRequest.Parameters.Add("!request", WebCrawlerHelper.GetRequest(e));
					onRequest.Parameters.Add("!PlaywrightRequest", e);

					var result = await callGoal.RunGoal(onRequest, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}

				};
			}

			if (onResponse != null)
			{
				browser.Response += async (sender, e) =>
				{

					onResponse.Parameters.Add("!sender", sender);
					onResponse.Parameters.Add("!request", WebCrawlerHelper.GetResponse(e));
					onResponse.Parameters.Add("!" + e.GetType().FullName, e);

					var result = await callGoal.RunGoal(onResponse, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}

				};
			}
			return browserInstance;
		}


		private async Task<IBrowserContext> GetBrowserType(IPlaywright playwright, string browserType, bool headless,
			string profileName, bool kioskMode, Dictionary<string, object>? argumentOptions, bool hideTestingMode)
		{
			switch (browserType)
			{
				case "Edge":
					throw new NotImplementedException("Not implemented for Safari. You can help building plang https://github.com/PLangHQ");
				case "Firefox":
					throw new NotImplementedException("Not implemented for Firefox. You can help building plang https://github.com/PLangHQ");
				case "Safari":
					throw new NotImplementedException("Not implemented for Safari. You can help building plang https://github.com/PLangHQ");
				default:
					return await GetChromeDriver(playwright, headless, profileName, kioskMode, argumentOptions, hideTestingMode);
			}
		}


		private async Task<BrowserInstance> GetBrowser(string browserType = "Chrome", bool headless = false, string profileName = "", string userSessionPath = "",
			bool incognito = false, bool kioskMode = false, Dictionary<string, object>? argumentOptions = null, int? timeoutInSeconds = null, bool hideTestingMode = false,
			GoalToCallInfo? onRequest = null, GoalToCallInfo? onResponse = null)
		{
			var browserInstance = await GetBrowserInstance(browserType, headless, profileName, kioskMode, argumentOptions, timeoutInSeconds, hideTestingMode, onRequest, onRequest );
			if (browserInstance != null) return browserInstance;

			return await StartBrowser(browserType, headless, userSessionPath, kioskMode, argumentOptions, timeoutInSeconds);
		}

		[Description("Get elements from page by cssSelector, when cssSelector is null it uses that last element accessed. parentElement is either cssSelector or an PlangWebElement being references")]
		public async Task<(List<PlangWebElement>?, IError?)> GetElements(string? cssSelector = null, object? parentElement = null)
		{
			var plangParentElement = parentElement as PlangWebElement;
			if (parentElement != null && plangParentElement == null)
			{
				(plangParentElement, var error) = await GetElement(parentElement.ToString());
				if (error != null) return (null, error);
			}

			
			var result = await GetWebElements(cssSelector, plangParentElement);
			if (result.Error != null) return (null, result.Error);
			if (result.Elements == null || result.Elements.Count == 0) return (new(), null);

			List<PlangWebElement> elements = new();
			foreach (var element in result.Elements)
			{
				var plangElement = await GetPlangWebElement(element);
				if (plangElement != null) elements.Add(plangElement);
			}
			return (elements, null);
		}

		[Description("Gets one element from the dom by cssSelector, when cssSelector is null it uses that last element accessed. parentElement is either cssSelector or an PlangWebElement being references. position=null|first|last|idx|%variable%, idx is a number")]
		public async Task<(PlangWebElement?, IError?)> GetElement(string? cssSelector = null, object? parentElement = null, string? position = null)
		{
			var result = await GetWebElements(cssSelector);
			if (result.Error != null) return (null, result.Error);

			IElementHandle element = result.Elements.FirstOrDefault();
			if (position != null)
			{
				if (position.Equals("last", StringComparison.OrdinalIgnoreCase))
				{
					element = result.Elements.LastOrDefault();
				}
				else if (int.TryParse(position, out int idx))
				{
					element = result.Elements.ElementAt(idx);
				}
			}
			


			return (await GetPlangWebElement(element), null);
		}

		private async Task<(IReadOnlyList<IElementHandle>? Elements, IError? Error)> GetWebElements(string cssSelector, PlangWebElement? parentElement = null)
		{
			if (cssSelector == null) cssSelector = await GetCssSelector();

			var page = await GetPage();

			var elements = await page.QuerySelectorAllAsync(cssSelector);
			if (elements == null) return (null, new ProgramError($"Element {cssSelector} does not exist.", goalStep, function));

			SetCssSelector(cssSelector);

			return (elements, null);


		}

		public async Task CloseBrowser()
		{
			var browser = data.BrowserInstance;
			if (browser == null) return;

			await browser.Browser.CloseAsync();
			data.BrowserInstance = null;
			
			
		}


		public async Task WaitForUrl(string expectedUrl, int timeoutInSeconds)
		{
			var page = await GetPage();

			await page.WaitForURLAsync(expectedUrl, new PageWaitForURLOptions() { Timeout = timeoutInSeconds * 1000 });
		}

		public async Task<IPage> GetPage(int idx = -1)
		{
			var browserInstance = await GetBrowserInstance();
			return await browserInstance.GetCurrentPage(idx);
		}


		[Description("opens a page to a url. browserType=Chrome|Edge|Firefox|IE|Safari. hideTestingMode tries to disguise that it is a bot.")]
		public async Task NavigateToUrl(string url, string browserType = "Chrome", bool headless = false,
				string profileName = "", bool kioskMode = false, Dictionary<string, object>? argumentOptions = null,
				int? timeoutInSeconds = null, bool hideTestingMode = false, int pageIndex = -1,
				GoalToCallInfo? onRequest = null, GoalToCallInfo? onResponse = null, GoalToCallInfo? onWebsocketReceived = null, GoalToCallInfo? onWebsocketSent = null,
				GoalToCallInfo? onConsoleOutput = null, GoalToCallInfo? onWorker = null,
				GoalToCallInfo? onDialog = null, GoalToCallInfo? onLoad = null, GoalToCallInfo? onDOMLoad = null, GoalToCallInfo? onFileChooser = null,
				GoalToCallInfo? onIFrameLoad = null, GoalToCallInfo? onDownload = null)
		{
			if (string.IsNullOrEmpty(url))
			{
				throw new RuntimeException("url cannot be empty");
			}
			this.engine.Name = url;

			if (!url.StartsWith("http"))
			{
				url = "https://" + url;
			}

			var pageGotoOptions = new PageGotoOptions();
			pageGotoOptions.WaitUntil = WaitUntilState.DOMContentLoaded;
			if (timeoutInSeconds != null)
			{
				pageGotoOptions.Timeout = timeoutInSeconds.Value * 1000;
			}

			var browser = GetBrowserInstance(browserType, headless, profileName, kioskMode, argumentOptions, timeoutInSeconds, hideTestingMode, onRequest, onResponse);
			IPage page = await GetPage(pageIndex);
			BindEventsToPage(page, url, onRequest, onResponse, onWebsocketReceived, onWebsocketSent,
					onConsoleOutput, onWorker,
					onDialog, onLoad, onDOMLoad, onFileChooser,
					onIFrameLoad, onDownload);


			var response = await page.GotoAsync(url, pageGotoOptions);
			data.Response = response;

			var wrappedResponse = await WebCrawlerHelper.GetResponse(response);
			memoryStack.Put(goal.GoalName + ".response", wrappedResponse, goalStep: goalStep);


		}

		public async Task<object> ExtractClassesToList(string[] cssSelectors, string fromCssSelector)
		{
			var page = await GetPage();
			var elements = await page.QuerySelectorAllAsync(fromCssSelector);
			List<string> classes = new List<string>();
			foreach (var element in elements)
			{
				foreach (var cssSelector in cssSelectors)
				{
					var classList = await element.EvaluateAsync<string[]>($@"
const rows = document.querySelectorAll(%fromCssSelector%);
const cssSelectors = %cssSelectors%;
const result = Array.from(rows).map(tr => {{
    const obj = {{}};
    cssSelectors.forEach(({{ key, selector }}) => {{
        obj[key] = tr.querySelector(selector)?.textContent?.trim() || '';
        obj['html'] = tr.querySelector(selector)?.innerHTML?.trim() || '';
        obj['outerHtml'] = tr.querySelector(selector)?.innerHTML?.trim() || '';
    }});
    return obj;
}}).filter(obj => Object.values(obj).some(val => val)); 
return result;");
					if (classList != null)
					{
						classes.AddRange(classList);
					}
				}
			}
			return classes;
		}

		public async Task ScrollToBottom()
		{
			var page = await GetPage();
			await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight);");
		}
		public async Task ScrollToElementByCssSelector(string cssSelector)
		{
			var page = await GetPage();
			var element = page.QuerySelectorAsync(cssSelector);
			await page.EvaluateAsync("(element) => { element.scrollIntoView(true);", element);
		}

		public async Task ScrollToElement(PlangWebElement element)
		{
			var page = await GetPage();
			await page.EvaluateAsync("(element) => { element.scrollIntoView(true);", element.WebElement);
		}

		[Description("operatorOnText can be equals|contains|startswith|endswith")]
		public async Task<PlangWebElement?> GetElementByText(string text, string operatorOnText = "equals", int? timeoutInSeconds = null, string? cssSelector = null)
		{
			var page = await GetPage();

			string escapedText = Regex.Escape(text);
			string pattern = operatorOnText switch
			{
				"equals" => $"^{escapedText}$",
				"startswith" => $"^{escapedText}",
				"endswith" => $"{escapedText}$",
				_ => escapedText
			};

			var locator = page.Locator($"text=/{pattern}/i");
			await locator.First.WaitForAsync();
			var element = await locator.First.ElementHandleAsync();

			if (element != null) return await GetPlangWebElement(element);
			return null;
		}


		public async Task WaitForElementToAppear(string cssSelector, int timeoutInSeconds = 30, bool waitForElementToChange = false)
		{
			var timeoutMs = timeoutInSeconds * 1000;
			var page = await GetPage();

			var locator = page.Locator(cssSelector);
			await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeoutMs });

			if (waitForElementToChange)
			{
				// Get the original element content
				var originalContent = await locator.InnerHTMLAsync();

				// Wait for the element content to change
				await page.WaitForFunctionAsync(
					$"selector => document.querySelector(selector)?.innerHTML !== {EscapeJsString(originalContent)}",
					cssSelector,
					new PageWaitForFunctionOptions { Timeout = timeoutMs }
				);
			}
		}

		private static string EscapeJsString(string value)
		{
			return value.Replace("'", "\\'").Replace("\"", "\\\"");
		}

		public async Task SetFocus(string? cssSelector = null, int? timoutInSeconds = null)
		{
			var page = await GetPage();
			cssSelector = await GetCssSelector(cssSelector);

			await page.FocusAsync(cssSelector, new PageFocusOptions() { Timeout = timoutInSeconds * 1000 });
		}

		public async Task ClickOnElement(PlangWebElement element)
		{
			await element.WebElement.ClickAsync();
		}

		public async Task Click(string cssSelector, int elementAtToClick = 0, bool clickAllMatchingElements = false, int? timeoutInSeconds = null)
		{
			var page = await GetPage();
			var elements = await page.QuerySelectorAllAsync(cssSelector);
			if (clickAllMatchingElements)
			{
				foreach (var element in elements)
				{
					await element.ClickAsync(new ElementHandleClickOptions() { Timeout = timeoutInSeconds * 1000 });
				}
			}
			else
			{
				var element = elements[elementAtToClick];
				await element.ClickAsync(new ElementHandleClickOptions() { Timeout = timeoutInSeconds * 1000 });
			}

		}
		/*
		 * 
		 **/
		public async Task<string?> AcceptAlert()
		{
			var page = await GetPage();
			List<IDialog>? dialogs = data.Dialogs;
			if (dialogs.Count == 0) return null;

			string message = "";
			foreach (var dialog in dialogs)
			{
				message = dialog.Message;
				await dialog.AcceptAsync();
				dialogs.Remove(dialog);
			}

			data.Dialogs = null;

			return message;
		}



		[Description("url can be regex. when user does not define url match all with url=**/*")]
		public async Task RemoveHeaderFromRequest(string url, List<string> headersToRemove)
		{
			var browserInstance = await GetBrowserInstance();
			var routeAsyncUrls = browserInstance.RouteAsyncByUrl;
			if (!routeAsyncUrls.TryGetValue(url, out RouteAsync? route))
			{
				route = new RouteAsync(headersToRemove);
				routeAsyncUrls.Add(url, route);
			}
			else
			{
				route.HeadersToRemove.AddRange(headersToRemove);
				routeAsyncUrls[url] = route;
			}

			browserInstance.RouteAsyncByUrl = routeAsyncUrls;
		}


		private async Task<string> GetCssSelector(string? cssSelector = null)
		{
			if (!string.IsNullOrEmpty(cssSelector)) return cssSelector;

			if (string.IsNullOrEmpty(cssSelector) && context.ContainsKey("prevCssSelector"))
			{
				cssSelector = context["prevCssSelector"]?.ToString();
			}

			if (string.IsNullOrEmpty(cssSelector))
			{
				cssSelector = "html";
			}

			return cssSelector;
		}
		private void SetCssSelector(string? cssSelector)
		{
			appContext.AddOrReplace("prevCssSelector", cssSelector);
		}

		[Description("Writes a text to an element")]
		public async Task SendKey(string value, string? cssSelector = null, int? timeoutInSeconds = null, bool humanStyle = false)
		{
			var page = await GetPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			if (humanStyle)
			{
				await element.TypeAsync(value, new ElementHandleTypeOptions() { Timeout = timeoutInSeconds * 1000 });
			}
			else
			{
				await element.FillAsync(value, new ElementHandleFillOptions() { Timeout = timeoutInSeconds * 1000 });
			}
		}

		[Description("set the text of an element other than input by cssSelector")]
		public async Task SetTextOnElement(string text, string? cssSelector = null, int? timeoutInSeconds = null, bool clearElementFirst = false)
		{
			await SendKey(text, cssSelector, timeoutInSeconds);
		}

		[Description("set the value of an input by cssSelector")]
		public async Task Input(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			await SendKey(value, cssSelector, timeoutInSeconds);
		}

		[Description("select an option by its value in select input by cssSelector")]
		public async Task SelectByValue(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			var page = await GetPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			await element.SelectOptionAsync(new SelectOptionValue() { Value = value });
			SetCssSelector(cssSelector);
		}

		[Description("select an option by its text in select input by cssSelector")]
		public async Task SelectByText(string text, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			var page = await GetPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			await element.SelectOptionAsync(new SelectOptionValue() { Label = text });
			SetCssSelector(cssSelector);
		}

		public async Task Submit(string? cssSelector = null, int? timeoutInSeconds = null)
		{
			var page = await GetPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			await element.ClickAsync();

			SetCssSelector(cssSelector);
		}
		private async Task<List<PlangWebElement>> GetPlangWebElements(IReadOnlyList<IElementHandle> elements)
		{
			List<PlangWebElement> plangElements = new();
			foreach (var element in elements)
			{
				var e = await GetPlangWebElement(element);
				plangElements.Add(e);
			}
			return plangElements;
		}

		private async Task<PlangWebElement?> GetPlangWebElement(IElementHandle? element)
		{
			if (element == null) return null;
			var plangWebElement = new PlangWebElement();
			plangWebElement.Text = await element.TextContentAsync();

			plangWebElement.TagName = (await element.EvaluateAsync<string>("el => el.tagName")).ToLower();
			plangWebElement.WebElement = element;
			plangWebElement.InnerHtml = await element.InnerHTMLAsync();
			plangWebElement.InnerText = await element.InnerTextAsync();

			return plangWebElement;
		}



		public async Task<List<PlangWebElement>> GetElements(string? cssSelector = null, string? shadowDomCssSelector = null)
		{
			var page = await GetPage();
			cssSelector = await GetCssSelector();

			var elements = await page.QuerySelectorAllAsync(cssSelector);
			return await GetPlangWebElements(elements);
		}

		public async Task<(List<PlangWebElement>?, IError?)> GetElementsInsideElement(string elementName, IElementHandle element)
		{
			if (element == null) return (null, new ProgramError("You must send in element to look inside", goalStep, function));

			var page = await GetPage();
			var elements = await element.QuerySelectorAllAsync(elementName);

			return (await GetPlangWebElements(elements), null);
		}

		public async Task<string?> FindElementAndExtractAttribute(string attribute, string? cssSelector = null, PlangWebElement? element = null)
		{

			var page = await GetPage();

			IElementHandle? e;
			if (element != null)
			{
				e = await element.WebElement.QuerySelectorAsync(cssSelector);
			}
			else
			{
				cssSelector = await GetCssSelector(cssSelector);
				e = await page.QuerySelectorAsync(cssSelector);
			}
			if (e == null) return null;

			return await e.GetAttributeAsync(attribute);

		}

		[Description("When cssSelector is null, all html is retrieved from page. outputFormat=html|md")]
		public async Task<(string?, IError?)> ExtractContent(string? cssSelector = null, PlangWebElement? element = null, string outputFormat = "html")
		{
			var page = await GetPage();

			string html;
			if (string.IsNullOrWhiteSpace(cssSelector))
			{
				html = await page.ContentAsync();
			}
			else
			{
				cssSelector = await GetCssSelector(cssSelector);
				var selectorElement = await page.QuerySelectorAsync(cssSelector);
				if (selectorElement == null)
				{
					return (null, new ProgramError($"{cssSelector} could not be found on page", Key: "CssSelectorNotFound"));
				}
				html = await selectorElement.InnerHTMLAsync();
			}

			outputFormat = outputFormat.TrimStart('.');
			if (outputFormat == "md")
			{
				var htmlDoc = new HtmlDocument();
				htmlDoc.LoadHtml(html);
				foreach (var node in htmlDoc.DocumentNode.DescendantsAndSelf())
				{
					if (node.NodeType == HtmlNodeType.Element)
					{
						HtmlNode prev = node.PreviousSibling;
						HtmlNode next = node.NextSibling;

						if (prev != null && prev.NodeType == HtmlNodeType.Text && node.NodeType == HtmlNodeType.Element)
							prev.InnerHtml = prev.InnerHtml.TrimEnd() + " ";

						if (next != null && next.NodeType == HtmlNodeType.Text)
							next.InnerHtml = " " + next.InnerHtml.TrimStart();
					}
				}


				var config = new ReverseMarkdown.Config
				{
					// Include the unknown tag completely in the result (default as well)
					UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
					// generate GitHub flavoured markdown, supported for BR, PRE and table tags
					GithubFlavored = true,
					// will ignore all comments
					RemoveComments = true,
					// remove markdown output for links where appropriate
					SmartHrefHandling = true,
				};
				var converter = new ReverseMarkdown.Converter(config);
				html = converter.Convert(htmlDoc.DocumentNode.OuterHtml);
			}

			SetCssSelector(cssSelector);
			return (html, null);
		}
		public async Task SwitchTab(int tabIndex)
		{
			await GetPage(tabIndex);
		}


		public async Task<Uri> GetUriFromPage(int? tabIndex = null)
		{
			var page = await GetPage(idx: tabIndex ?? -1);
			return new Uri(page.Url);
		}

		public async Task AddDefaultRequestHeader(Dictionary<string, object> headers)
		{
			var browserInstance = await GetBrowserInstance();
			await browserInstance.Browser.SetExtraHTTPHeadersAsync(headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()!));
		}

		[Description("keys of the header, find by value where operation (startwith|endwith|equals|contains), request object can be null, will use the pages request")]
		public async Task<(object?, IError?)> GetRequestHeaders(List<string>? keys = null, string keyOperator = "equals", string? value = null, string? valueOperator = "contains")
		{
			Dictionary<string, string> headers;
			var request = data.Request;

			if (request == null)
			{
				return (null, new ProgramError("Could not find a request object. Have you loaded a page?", goalStep, function, FixSuggestion: "Call `- navigate to example.org` before getting headers"));
			}

			headers = request.Headers;
			var dict = OperatorHelper.ApplyOperator(headers, keys, keyOperator, value, valueOperator);
			if (keys != null && keys.Count == 1 && dict != null && dict.Count == 1)
			{
				return (dict.FirstOrDefault().Value, null);
			}
			return (dict, null);

		}

		[Description("key of the header, find by value where operation (startwith|endwith|equals|contains), request object can be null, will use the pages request")]
		public async Task<(object?, IError?)> GetResponseHeaders(List<string>? keys = null, string keyOperator = "equals", string? value = null, string? valueOperator = "contains")
		{
			Dictionary<string, string> headers;
			var response = data.Response;

			if (response == null)
			{
				return (null, new ProgramError("Could not find a response object. Have you loaded a page?", goalStep, function, FixSuggestion: "Call `- navigate to example.org` before getting headers"));
			}

			var dict = OperatorHelper.ApplyOperator(response.Headers, keys, keyOperator, value, valueOperator);
			if (keys != null && keys.Count == 1 && dict != null && dict.Count == 1) {
				return (dict.FirstOrDefault().Value, null);
			}
			return (dict, null);
		}

		public async Task Wait(int milliseconds = 1000)
		{
			await Task.Delay(milliseconds);
		}

		public async Task<IError?> TakeScreenshotOfWebsite(string saveToPath, bool overwrite = false, string? cssSelector = null)
		{
			if (string.IsNullOrWhiteSpace(saveToPath))
			{
				return new ProgramError("The path where to save the screenshot cannot be empty", goalStep, function);
			}

			var absolutePath = GetPath(saveToPath);
			var folderPath = Path.GetDirectoryName(absolutePath);
			if (!fileSystem.Directory.Exists(folderPath))
			{
				fileSystem.Directory.CreateDirectory(folderPath);
			}

			if (!overwrite && fileSystem.File.Exists(absolutePath))
			{
				return new ProgramError("File exists and will not be overwritten.", goalStep, function, FixSuggestion: $"Rewrite your step to include that you want to overwrite the file, e.g. `- {goalStep.Text}, overwrite`");
			}

			var page = await GetPage();
			if (cssSelector != null)
			{
				var element = await page.QuerySelectorAsync(cssSelector);
				if (element == null)
				{
					return new ProgramError($"The element {cssSelector} could not be found on page.", goalStep, function);
				}
				await element.ScreenshotAsync(new ElementHandleScreenshotOptions() { Path = absolutePath });
			}
			else
			{
				await page.ScreenshotAsync(new PageScreenshotOptions() { Path = absolutePath });
			}


			return null;
		}

		private string ConvertKeyCommand(string value)
		{
			if (value == "\\t") return "\t";
			if (value == "\\r") return "\r";
			if (value == "\\n") return "\n";
			return value;
		}




		private string GetChromeProfileFolder(string profileName)
		{
			string localStatePath = fileSystem.Path.Join(GetChromeUserDataDir(), "Local State");

			string localStateContent = File.ReadAllText(localStatePath);
			JObject localStateJson = JObject.Parse(localStateContent);

			var profileInfo = localStateJson["profile"]?["info_cache"];
			if (profileInfo == null)
			{
				throw new InvalidOperationException("Profile info not found in Local State file.");
			}

			foreach (JToken profile in profileInfo)
			{
				var property = profile as JProperty;
				if (property == null) continue;


				string? name = property.Value["name"]?.ToString();

				if (string.Equals(profileName, name, StringComparison.OrdinalIgnoreCase))
				{
					return property.Name;
				}
			}

			throw new InvalidOperationException("Profile info not found in Local State file.");
		}
		private BrowserTypeLaunchOptions GetChromeIcognitoOptions(bool headless, bool kioskMode, Dictionary<string, object>? argumentOptions, bool hideTestingMode)
		{
			BrowserTypeLaunchOptions options = new BrowserTypeLaunchOptions();
			options.Headless = headless;
			List<string> args = new();
			if (hideTestingMode)
			{
				args.Add("--disable-blink-features=AutomationControlled");
			}
			if (kioskMode)
			{
				args.Add("kios");
			}
			if (hideTestingMode)
			{
				args.Add("--disable-blink-features=AutomationControlled");
				//options..UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";

			}
			if (argumentOptions != null)
			{
				foreach (var argument in argumentOptions)
				{
					args.Add(argument.Key + "=" + argument.Value);
				}
			}
			options.Args = args.ToArray();
			return options;
		}

		private BrowserTypeLaunchOptions GetChromeOptions(bool headless, bool kioskMode, Dictionary<string, object>? argumentOptions, bool hideTestingMode)
		{
			var options = new BrowserTypeLaunchOptions();
			options.Headless = headless;

			List<string> args = new();

			if (kioskMode)
			{
				args.Add("kios");
			}
			if (argumentOptions != null)
			{
				foreach (var argument in argumentOptions)
				{
					args.Add(argument.Key + "=" + argument.Value);
				}
			}
			options.Args = args.ToArray();
			return options;
		}

		private BrowserTypeLaunchPersistentContextOptions GetChromeOptionsPersistent(bool headless, bool kioskMode, Dictionary<string, object>? argumentOptions, bool hideTestingMode)
		{
			var options = new BrowserTypeLaunchPersistentContextOptions();
			options.Headless = headless;

			List<string> args = new();

			if (kioskMode)
			{
				args.Add("kios");
			}
			if (argumentOptions != null)
			{
				foreach (var argument in argumentOptions)
				{
					args.Add(argument.Key + "=" + argument.Value);
				}
			}
			options.Args = args.ToArray();
			return options;
		}
		private async Task<IBrowserContext> GetChromeDriver(IPlaywright playwright, bool headless, string profileName,
			bool kioskMode, Dictionary<string, object>? argumentOptions, bool hideTestingMode, int errorCount = 0)
		{
			string? userProfile = null;

			if (profileName.Equals("default", StringComparison.OrdinalIgnoreCase))
			{
				userProfile = GetChromeUserDataDir();
				logger.LogDebug($"Using user path: {userProfile}");
			}
			else if (!string.IsNullOrEmpty(profileName))
			{
				userProfile = GetChromeProfileFolder(profileName);
				logger.LogDebug($"Using user path: {userProfile}");
			}

			var argHasUserAgent = (argumentOptions != null && argumentOptions.FirstOrDefault(p => p.Key.Equals("user-agent", StringComparison.OrdinalIgnoreCase)).Value != null);

			IBrowserContext browser;
			if (userProfile == null)
			{
				var options = GetChromeIcognitoOptions(headless, kioskMode, argumentOptions, hideTestingMode);
				try
				{
					options.DownloadsPath = fileSystem.Path.Join(fileSystem.RootDirectory, "Downloads");
					var chromium = await playwright.Chromium.LaunchAsync(options);
					var contextOptions = new BrowserNewContextOptions();
					if (hideTestingMode && !argHasUserAgent)
					{
						contextOptions.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
					}

					browser = await chromium.NewContextAsync(contextOptions);

				}
				catch (PlaywrightException pe)
				{
					if (errorCount < 2 && pe.Message.Contains("Executable doesn't exist"))
					{
						var program = programFactory.GetProgram<PLang.Modules.TerminalModule.Program>(goalStep);
						await program.RunTerminal("playwright.ps1", ["install"], pathToWorkingDirInTerminal: AppContext.BaseDirectory);

						return await GetChromeDriver(playwright, headless, profileName, kioskMode, argumentOptions, hideTestingMode, ++errorCount);
					}
					throw;
				}
			}
			else
			{
				try
				{
					var options = GetChromeOptionsPersistent(headless, kioskMode, argumentOptions, hideTestingMode);
					if (hideTestingMode && !argHasUserAgent)
					{
						//options.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
					}
					var tempProfileFolder = GetPath("/.chrome");
					var profileTemp = Path.Join(tempProfileFolder, userProfile);


					var path = GetChromeUserDataDir();
					var profileDir = Path.Join(path, userProfile);
					CopyDirectory(profileDir, profileTemp);


					options.Channel = "chrome";
					//options.Args = options.Args!.Append($"--user-data-dir={path}");
					options.Args = options.Args!.Append($"--profile-directory={userProfile}");

					if (hideTestingMode)
					{
						options.Args = options.Args!.Append($"--disable-blink-features=AutomationControlled");
						options.Args = options.Args!.Append($"--disable-blink-features");
						options.Args = options.Args!.Append($"--disable-infobars");
					}
					if (hideTestingMode && !argHasUserAgent)
					{
						options.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
					}


					browser = await playwright.Chromium.LaunchPersistentContextAsync(tempProfileFolder, options);

				}
				catch (PlaywrightException pe)
				{
					if (pe.Message.Contains("Executable doesn't exist"))
					{
						var program = programFactory.GetProgram<PLang.Modules.TerminalModule.Program>(goalStep);
						await program.RunTerminal("playwright.ps1", ["instal"]);

						return await GetChromeDriver(playwright, headless, profileName, kioskMode, argumentOptions, hideTestingMode);
					}
					throw;
				}
			}

			if (hideTestingMode)
			{
				await browser.AddInitScriptAsync(@"Object.defineProperty(navigator, 'webdriver', {
            get: () => undefined
        });");
			}
			browser.Close += async (object? sender, IBrowserContext e) =>
			{
				//await CloseBrowser();
				int i = 0;
			};
			return browser;
		}

		private void CopyDirectory(string sourceDir, string targetDir)
		{
			var source = new DirectoryInfo(sourceDir);
			if (!source.Exists)
				throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");

			fileSystem.Directory.CreateDirectory(targetDir);

			foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
			{
				var relativePath = Path.GetRelativePath(source.FullName, file.FullName);
				var destFile = Path.Combine(targetDir, relativePath);
				fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
				if (!fileSystem.File.Exists(destFile))
				{
					file.CopyTo(destFile, false);
				}
			}
		}
		private bool IsUrlMatch(string pageUrl, string? eventUrl)
		{
			if (eventUrl == null) return false;

			string cleanPageUrl = CleanUrl(pageUrl);
			string cleanEventUrl = CleanUrl(eventUrl);

			return (cleanPageUrl.Equals(cleanEventUrl, StringComparison.OrdinalIgnoreCase));
		}

		private string CleanUrl(string url)
		{
			return url.Replace("http://", "").Replace("https://", "").TrimEnd('/');
		}

		private void BindEventsToPage(IPage page, string pageUrl, GoalToCallInfo? onRequest = null, GoalToCallInfo? onResponse = null, GoalToCallInfo? onWebsocketReceived = null, GoalToCallInfo? onWebsocketSent = null,
			GoalToCallInfo? onConsoleOutput = null, GoalToCallInfo? onWorker = null, GoalToCallInfo? onDialog = null, GoalToCallInfo? onLoad = null,
			GoalToCallInfo? onDOMLoad = null, GoalToCallInfo? onFileChooser = null, GoalToCallInfo? onIFrameLoad = null, GoalToCallInfo? onDownload = null)
		{
			var callGoal = programFactory.GetProgram<CallGoalModule.Program>(goalStep);

			page.Console += async (object? sender, IConsoleMessage e) =>
			{
				if (IsUrlMatch(pageUrl, e.Page?.Url))
				{
					data.Console = e;
				}
				if (onConsoleOutput != null)
				{
					onConsoleOutput.Parameters.Add("!sender", sender);
					onConsoleOutput.Parameters.Add("!console", e);
					var result = await callGoal.RunGoal(onConsoleOutput, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				}
			};


			page.Request += async (object? sender, IRequest e) =>
			{
				if (IsUrlMatch(pageUrl, e.Url))
				{
					data.Request = e;
				}

				if (onRequest != null)
				{
					onRequest.Parameters.Add("!sender", sender);
					onRequest.Parameters.Add("!request", WebCrawlerHelper.GetRequest(e));
					onRequest.Parameters.Add("!" + e.GetType().FullName, e);
					var result = await callGoal.RunGoal(onRequest, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				}
			};


			page.Response += async (object? sender, Microsoft.Playwright.IResponse e) =>
			{
				if (IsUrlMatch(pageUrl, e.Url))
				{
					data.Response = e;
				}

				if (onResponse != null)
				{
					var response = await WebCrawlerHelper.GetResponse(e);
					if (response == null) return;

					onResponse.Parameters.Add("!sender", sender);
					onResponse.Parameters.Add("!response", response);
					var result = await callGoal.RunGoal(onResponse, isolated: true);

					if (result.Error != null)
					{
						Console.WriteLine("page.Response:" + result.Error);
						throw new ExceptionWrapper(result.Error);
					}
				}
			};



			if (onWebsocketReceived != null || onWebsocketSent != null)
			{
				page.WebSocket += (object? sender, IWebSocket e) =>
				{
					if (onWebsocketReceived != null)
					{
						e.FrameReceived += async (object? sender, IWebSocketFrame e) =>
						{
							onWebsocketReceived.Parameters.Add("!sender", sender);
							onWebsocketReceived.Parameters.Add("!websocket", e);

							var result = await callGoal.RunGoal(onWebsocketReceived, isolated: true);
							if (result.Error != null)
							{
								throw new ExceptionWrapper(result.Error);
							}
						};
					}
					if (onWebsocketSent != null)
					{
						e.FrameSent += async (object? sender, IWebSocketFrame e) =>
						{
							onWebsocketSent.Parameters.Add("!sender", sender);
							onWebsocketSent.Parameters.Add("!websocket", e);
							var result = await callGoal.RunGoal(onWebsocketSent, isolated: true);

							if (result.Error != null)
							{
								throw new ExceptionWrapper(result.Error);
							}
						};
					}


				};
			}

			if (onWorker != null)
			{
				page.Worker += async (object? sender, IWorker e) =>
				{
					onWorker.Parameters.Add("!sender", sender);
					onWorker.Parameters.Add("!worker", e);

					var result = await callGoal.RunGoal(onWorker, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				};
			}



			page.Dialog += async (object? sender, IDialog e) =>
			{
				if (IsUrlMatch(pageUrl, e.Page?.Url))
				{
					var dialogs = data.Dialogs ?? new();
					dialogs.Add(e);
					data.Dialogs = dialogs;
				}
				if (onDialog != null)
				{
					onDialog.Parameters.Add("!sender", sender);
					onDialog.Parameters.Add("!dialog", e);
					var result = await callGoal.RunGoal(onDialog, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				}
			};

			if (onLoad != null)
			{
				page.Load += async (object? sender, IPage e) =>
				{
					onLoad.Parameters.Add("!sender", sender);
					onLoad.Parameters.Add("!page", e);
					var result = await callGoal.RunGoal(onLoad, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}

				};
			}

			if (onDOMLoad != null)
			{
				page.DOMContentLoaded += async (object? sender, IPage e) =>
				{
					onDOMLoad.Parameters.Add("!sender", sender);
					onDOMLoad.Parameters.Add("!page", e);

					var result = await callGoal.RunGoal(onDOMLoad, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				};
			}


			page.FileChooser += async (object? sender, IFileChooser e) =>
			{

				if (IsUrlMatch(pageUrl, e.Page.Url))
				{
					data.FileChooser = e;
				}

				if (onFileChooser != null)
				{
					onFileChooser.Parameters.Add("!sender", sender);
					onFileChooser.Parameters.Add("!filechooser", e);
					var result = await callGoal.RunGoal(onFileChooser, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				}
			};

			if (onIFrameLoad != null)
			{
				page.FrameNavigated += async (object? sender, IFrame e) =>
				{
					onIFrameLoad.Parameters.Add("!sender", sender);
					onIFrameLoad.Parameters.Add("!iframe", e);

					var result = await callGoal.RunGoal(onIFrameLoad, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				};
			}

			page.Download += async (object? sender, IDownload e) =>
			{
				if (IsUrlMatch(pageUrl, e.Page.Url))
				{
					data.Download = e;
				}
				if (onDownload != null)
				{
					onDownload.Parameters.Add("!sender", sender);
					onDownload.Parameters.Add("!download", e);

					var result = await callGoal.RunGoal(onDownload, isolated: true);

					if (result.Error != null)
					{
						throw new ExceptionWrapper(result.Error);
					}
				}
			};

		}

		
	}
}
