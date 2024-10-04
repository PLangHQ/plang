using NBitcoin.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Safari;
using OpenQA.Selenium.Support.UI;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SeleniumExtras.WaitHelpers;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PLang.Modules.WebCrawlerModule
{
	[Description("Run a browser instance, browse a website, input values and click on html elements, sendkeys, wait for browser and extract content")]
	public class Program : BaseProgram, IDisposable
	{
		WebDriver? driver = null;
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;

		public Program(PLangAppContext context, IPLangFileSystem fileSystem, ILogger logger) : base()
		{
			this.context = context;
			this.fileSystem = fileSystem;
			this.logger = logger;
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

		[Description("browserType=Chrome|Edge|Firefox|IE|Safari. hideTestingMode tries to disguise that it is a bot.")]
		public async Task StartBrowser(string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "",
			bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null, int timoutInSeconds = 30, bool hideTestingMode = false)
		{
			driver = GetBrowserType(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);

			driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(timoutInSeconds);
			driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(timoutInSeconds);
			driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timoutInSeconds);

			context.TryAdd("SeleniumBrowser", driver);
		}

		private WebDriver GetBrowserType(string browserType, bool headless, bool useUserSession,
			string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			switch (browserType)
			{
				case "Edge":
					return GetEdgeDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
				case "Firefox":
					return GetFirefoxDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
				case "Safari":
					return GetSafariDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
				default:
					return GetChromeDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
			}


		}


		private async Task<WebDriver> GetDriver(string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "",
			bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null, int timeoutInSeconds = 60)
		{
			if (!context.ContainsKey("SeleniumBrowser"))
			{
				logger.LogDebug("Key SeleniumBrowser not existing. Starting browser");
				await StartBrowser(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, timeoutInSeconds);
			}
			return context["SeleniumBrowser"] as WebDriver;
		}

		private async Task<IWebElement> GetElement(string? cssSelector = null)
		{
			var driver = await GetDriver();
			if (cssSelector == null) cssSelector = GetCssSelector();

			var element = driver.FindElement(By.CssSelector(cssSelector));


			if (element == null)
			{
				throw new Exception($"Element {cssSelector} does not exist.");
			}
			SetCssSelector(cssSelector);
			return element;
		}

		public async Task CloseBrowser()
		{
			if (!context.ContainsKey("SeleniumBrowser")) return;

			var driver = context["SeleniumBrowser"] as ChromeDriver;
			if (driver == null) return;

			driver.Quit();
			context.Remove("SeleniumBrowser");


		}

		[Description("browserType=Chrome|Edge|Firefox|IE|Safari. hideTestingMode tries to disguise that it is a bot.")]
		public async Task NavigateToUrl(string url, string browserType = "Chrome", bool headless = false, bool useUserSession = false,
				string userSessionPath = "", bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null,
				string? browserConsoleOutputVariableName = null, int timeoutInSecods = 30, bool hideTestingMode = false)
		{
			if (string.IsNullOrEmpty(url))
			{
				throw new RuntimeException("url cannot be empty");
			}

			var driver = await GetDriver(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, timeoutInSecods);
			if (!url.StartsWith("http"))
			{
				url = "https://" + url;
			}
			
			driver.Navigate().GoToUrl(url);

			var logs = driver.Manage().Logs.GetLog(LogType.Browser);
			if (browserConsoleOutputVariableName != null)
			{
				memoryStack.Put(browserConsoleOutputVariableName, logs);
			}
		}

		public async Task ScrollToBottom()
		{
			var driver = await GetDriver();
			IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
			js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
		}

		public async Task ScrollToElement(IWebElement element)
		{
			var driver = await GetDriver();
			IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
			js.ExecuteScript("arguments[0].scrollIntoView(true);", element);
		}

		public async Task WaitForElementToAppear(string cssSelector, int timoutInSeconds = 30)
		{
			var driver = await GetDriver();

			await SetTimeout(timoutInSeconds);

			WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timoutInSeconds));
			wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(cssSelector)));

			await ResetTimeout();

		}

		TimeSpan? originalAsyncJsTimeout = null;
		TimeSpan? originalPageLoad = null;
		TimeSpan? originalImplicitWait = null;
		private async Task ResetTimeout()
		{
			if (originalAsyncJsTimeout == null) return;

			var driver = await GetDriver();
			driver.Manage().Timeouts().AsynchronousJavaScript = originalAsyncJsTimeout ?? TimeSpan.FromSeconds(30);
			driver.Manage().Timeouts().PageLoad = originalPageLoad ?? TimeSpan.FromSeconds(30);
			driver.Manage().Timeouts().ImplicitWait = originalImplicitWait ?? TimeSpan.FromSeconds(30);
		}

		public async Task SetTimeout(int? timoutInSeconds = null)
		{
			if (timoutInSeconds == null) return;

			var driver = await GetDriver();
			originalAsyncJsTimeout = driver.Manage().Timeouts().AsynchronousJavaScript;
			originalPageLoad = driver.Manage().Timeouts().PageLoad;
			originalImplicitWait = driver.Manage().Timeouts().ImplicitWait;

			driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds((int) timoutInSeconds);
			driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds((int)timoutInSeconds);
			driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds((int)timoutInSeconds);
		}

		public async Task SetFocus(string? cssSelector = null, int? timoutInSeconds = null)
		{
			var driver = await GetDriver();
			try
			{
				await SetTimeout(timoutInSeconds);
				var element = await GetElement(cssSelector);
				new Actions(driver).MoveToElement(element).Perform();
			}
			finally
			{
				await ResetTimeout();
			}
		}

		public async Task Click(string cssSelector, int elementAtToClick = 0, bool clickAllMatchingElements = false, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				if (!clickAllMatchingElements)
				{
					var element = await GetElement(cssSelector);
					element.Click();
				}
				else
				{
					var elements = await GetElements(cssSelector);
					if (elementAtToClick != 0)
					{
						if (elements.Count >= elementAtToClick)
						{
							elements[elements.Count - 1].Click();
						}
					}
					foreach (var element in elements)
					{
						element.Click();
					}
				}
				SetCssSelector(cssSelector);
			}
			finally
			{
				await ResetTimeout();
			}
		}

		public async Task AcceptPrompt()
		{
			var driver = await GetDriver();
			IAlert alert = driver.SwitchTo().Alert();
			alert.Accept();
		}

		private string GetCssSelector(string? cssSelector = null)
		{
			if (string.IsNullOrEmpty(cssSelector) && context.ContainsKey("prevCssSelector"))
			{
				cssSelector = context["prevCssSelector"].ToString();
			}

			if (cssSelector == null)
			{
				var focusedElement = (IWebElement)((IJavaScriptExecutor)driver).ExecuteScript("return document.activeElement;");
				cssSelector = GetCssSelector(focusedElement, driver);
			}

			return cssSelector;
		}
		private void SetCssSelector(string? cssSelector)
		{
			context.AddOrReplace("prevCssSelector", cssSelector);
		}

		public async Task SendKey(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = GetCssSelector(cssSelector);
				var element = await GetElement(cssSelector);
				var input = ConvertKeyCommand(value);
				element.SendKeys(input);
				SetCssSelector(cssSelector);
			}
			finally
			{
				await ResetTimeout();
			}
		}

		[Description("set the value of an input by cssSelector")]
		public async Task Input(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = GetCssSelector(cssSelector);
				var element = await GetElement(cssSelector);
				element.SendKeys(value);
				SetCssSelector(cssSelector);
			}
			finally
			{
				await ResetTimeout();
			}
		}

		[Description("select an option by its value in select input by cssSelector")]
		public async Task SelectByValue(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = GetCssSelector(cssSelector);
				var element = await GetElement(cssSelector);


				var selectElement = new SelectElement(element);
				selectElement.SelectByValue(value);
			}
			finally
			{
				await ResetTimeout();
			}

			SetCssSelector(cssSelector);
		}

		[Description("select an option by its text in select input by cssSelector")]
		public async Task SelectByText(string text, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = GetCssSelector(cssSelector);
				var element = await GetElement(cssSelector);
				var selectElement = new SelectElement(element);
				selectElement.SelectByText(text);
			}
			finally
			{
				await ResetTimeout();
			}
			SetCssSelector(cssSelector);
		}

		public async Task Submit(string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = GetCssSelector(cssSelector);
				var element = await GetElement(cssSelector);
				element.Submit();
			}
			finally
			{
				await ResetTimeout();
			}
			SetCssSelector(cssSelector);
		}
		/*
		public async Task<List<string>> ExtractContent(string content, string cssSelector, bool clearHtml = true)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(content);
			if (cssSelector.StartsWith("."))
			{
				cssSelector = cssSelector.Replace(" ", ".");
			}
			var nodes = doc.DocumentNode.SelectNodes(cssSelector);

			List<string> strings = new List<string>();
			foreach (var node in nodes)
			{
				if (clearHtml)
				{
					strings.Add(node.InnerHtml.ClearHtml());
				}
				else
				{
					strings.Add(node.InnerHtml);
				}
			}
			return strings;
		}
		*/

		public async Task<ReadOnlyCollection<IWebElement>> GetElements(string? cssSelector = null)
		{
			cssSelector = GetCssSelector(cssSelector);
			var driver = await GetDriver();
			var elements = driver.FindElements(By.CssSelector(cssSelector));

			return elements;
		}

		public async Task<string> FindElementAndExtractAttribute(string attribute, string? cssSelector = null, IWebElement? element = null)
		{
			cssSelector = GetCssSelector(cssSelector);
			List<string> results = new List<string>();

			ReadOnlyCollection<IWebElement> elements;
			if (element != null)
			{
				elements = element.FindElements(By.CssSelector(cssSelector));
			}
			else
			{
				var driver = await GetDriver();
				elements = driver.FindElements(By.CssSelector(cssSelector));
			}
			return "";
		}

		public async Task<List<string>> ExtractContent(bool clearHtml = true, string? cssSelector = null, IWebElement? element = null)
		{
			cssSelector = GetCssSelector(cssSelector);
			List<string> results = new List<string>();

			ReadOnlyCollection<IWebElement> elements;
			if (element != null)
			{
				elements = element.FindElements(By.CssSelector(cssSelector));
			}
			else
			{
				var driver = await GetDriver();
				elements = driver.FindElements(By.CssSelector(cssSelector));
			}
			foreach (var e in elements)
			{
				string text = (clearHtml) ? e.GetAttribute("innerText") : e.GetAttribute("outerHTML");
				results.Add(text);
			}
			SetCssSelector(cssSelector);
			return results;
		}
		public async Task SwitchTab(int tabIndex)
		{
			var driver = await GetDriver();
			ReadOnlyCollection<string> tabs = driver.WindowHandles;
			if (tabs.Count == tabIndex)
			{
				tabIndex--;
			}

			driver.SwitchTo().Window(tabs[tabIndex]);

		}

		public async Task Wait(int milliseconds = 1000)
		{
			await Task.Delay(milliseconds);
		}

		public async Task TakeScreenshotOfWebsite(string saveToPath)
		{
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(saveToPath)))
			{
				return;
			}

			var screenShot = driver.GetScreenshot();
			screenShot.SaveAsFile(saveToPath);
		}

		private string ConvertKeyCommand(string value)
		{
			if (value == "\\t") return "\t";
			if (value == "\\r") return "\r";
			if (value == "\\n") return "\n";
			return value;
		}

		public void Dispose()
		{
			CloseBrowser().Wait();
		}


		private string GetCssSelector(IWebElement element, IJavaScriptExecutor js)
		{
			string tagName = element.TagName;
			string id = element.GetAttribute("id");
			string classes = element.GetAttribute("class");

			if (!string.IsNullOrEmpty(id))
			{
				return $"#{id}";
			}
			else if (!string.IsNullOrEmpty(classes))
			{
				return $"{tagName}.{string.Join('.', classes.Split(' '))}";
			}
			else
			{
				// This is a more advanced approach: finding the nth-child index
				var index = js.ExecuteScript(
					"return Array.from(arguments[0].parentNode.children).indexOf(arguments[0]) + 1;",
					element
				);

				string parentSelector = GetCssSelector(element.FindElement(By.XPath("..")), js);
				return $"{parentSelector} > {tagName}:nth-child({index})";
			}
		}

		private SafariDriver GetSafariDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			var options = new SafariOptions();

			return new SafariDriver(options);
		}
		private FirefoxDriver GetFirefoxDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			FirefoxOptions options = new FirefoxOptions();
			if (useUserSession && string.IsNullOrEmpty(userSessionPath))
			{
				options.AddArgument(@$"user-data-dir={GetChromeUserDataDir()}");
			}
			if (headless)
			{
				options.AddArgument("headless");
			}
			if (incognito)
			{
				options.AddArgument("incognito");
			}

			if (kioskMode)
			{
				options.AddArgument("kios");
			}

			if (argumentOptions != null)
			{
				foreach (var args in argumentOptions)
				{
					options.AddArgument(args.Key + "=" + args.Value);
				}
			}
			return new FirefoxDriver(options);
		}
		private ChromiumDriver GetEdgeDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			var options = new EdgeOptions();
			if (useUserSession && string.IsNullOrEmpty(userSessionPath))
			{
				options.AddArgument(@$"user-data-dir={GetChromeUserDataDir()}");
			}
			if (headless)
			{
				options.AddArgument("headless");
			}
			if (incognito)
			{
				options.AddArgument("incognito");
			}

			if (kioskMode)
			{
				options.AddArgument("kios");
			}
			if (argumentOptions != null)
			{
				foreach (var args in argumentOptions)
				{
					options.AddArgument(args.Key + "=" + args.Value);
				}
			}

			return new EdgeDriver(options);
		}
		private ChromiumDriver GetChromeDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			ChromeOptions options = new ChromeOptions();

			if (useUserSession && string.IsNullOrEmpty(userSessionPath))
			{
				var path = GetChromeUserDataDir();
				logger.LogDebug($"Using user path: {path}");
				options.AddArgument(@$"user-data-dir={path}");
			} else if (!string.IsNullOrEmpty(userSessionPath))
			{
				options.AddArgument(@$"user-data-dir={userSessionPath}");
			}
			if (headless)
			{
				options.AddArgument("headless");
			}
			if (incognito)
			{
				options.AddArgument("incognito");
			}

			if (kioskMode)
			{
				options.AddArgument("kios");
			}
			if (argumentOptions != null)
			{
				foreach (var args in argumentOptions)
				{
					options.AddArgument(args.Key + "=" + args.Value);
				}
			}
			options.SetLoggingPreference(LogType.Browser, OpenQA.Selenium.LogLevel.All);

			if (hideTestingMode)
			{
				options.AddExcludedArgument("enable-automation");
				options.AddAdditionalOption("useAutomationExtension", false);
				
				options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
				options.AddExcludedArgument("enable-automation");

				options.AddArgument("--disable-blink-features=AutomationControlled");
			}

			var service = ChromeDriverService.CreateDefaultService();
			service.SuppressInitialDiagnosticInformation = true;
			service.HideCommandPromptWindow = true;

			var driver = new ChromeDriver(service, options);

			if (hideTestingMode) {
				// After initializing the driver
				IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

				// Override the navigator.webdriver property to undefined
				js.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
				js.ExecuteScript(@" 
    Object.defineProperty(navigator, 'plugins', { 
        get: () => [1, 2, 3], 
    }); 
    Object.defineProperty(navigator, 'languages', { 
        get: () => ['en-US', 'en'], 
    });
");
			}
			return driver;
		}

	}
}
