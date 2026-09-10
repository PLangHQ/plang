# WebCrawlerModule

Hint: `[webcrawler]`  
Type: `PLang.Modules.WebCrawlerModule.Program`

Run a browser instance, browse a website, input values and click on html elements, sendkeys, wait for browser and extract content

## Methods

### AcceptAlert

```
AcceptAlert() : String
```


### AddDefaultRequestHeader

```
AddDefaultRequestHeader(Dictionary<String, Object> headers = null) : object
```


### AssertPageContains

```
AssertPageContains(PLang.Modules.WebCrawlerModule.Program+AssertCheck assertCheck) : object
```

- `assertCheck` *PLang.Modules.WebCrawlerModule.Program+AssertCheck* — (see Type information in SupportingObjects)

### Click

```
Click(String cssSelector, Int32 elementAtToClick = 0, Boolean clickAllMatchingElements = False, Nullable<Int32> timeoutInSeconds = null) : object
```


### ClickOnElement

```
ClickOnElement(PLang.Modules.WebCrawlerModule.PlangWebElement element) : object
```

- `element` *PLang.Modules.WebCrawlerModule.PlangWebElement* — (see Type information in SupportingObjects)

### CloseBrowser

```
CloseBrowser() : object
```


### ExtractClassesToList

```
ExtractClassesToList(String[] cssSelectors, String fromCssSelector) : Object
```


### ExtractContent

```
ExtractContent(String cssSelector = null, PLang.Modules.WebCrawlerModule.PlangWebElement element = null, String outputFormat = html) : String
```

When cssSelector is null, all html is retrieved from page. outputFormat=html|md

- `cssSelector` *String*, default `null`
- `element` *PLang.Modules.WebCrawlerModule.PlangWebElement*, default `null` — (see Type information in SupportingObjects)
- `outputFormat` *String*, default `html`

### FindElementAndExtractAttribute

```
FindElementAndExtractAttribute(String attribute, String cssSelector = null, PLang.Modules.WebCrawlerModule.PlangWebElement element = null) : String
```

- `attribute` *String*
- `cssSelector` *String*, default `null`
- `element` *PLang.Modules.WebCrawlerModule.PlangWebElement*, default `null` — (see Type information in SupportingObjects)

### GetBrowserInstance

```
GetBrowserInstance(String browserType = Chrome, Boolean headless = False, String profileName = , Boolean kioskMode = False, Dictionary<String, Object> argumentOptions = null, Nullable<Int32> timoutInSeconds = null, Boolean hideTestingMode = False, PLang.Models.GoalToCallInfo onRequest = null, PLang.Models.GoalToCallInfo onResponse = null) : PLang.Modules.WebCrawlerModule.Models.BrowserInstance
```

- `browserType` *String*, default `Chrome`
- `headless` *Boolean*, default `False`
- `profileName` *String*, default ``
- `kioskMode` *Boolean*, default `False`
- `argumentOptions` *Dictionary<String, Object>*, default `null`
- `timoutInSeconds` *Nullable<Int32>*, default `null`
- `hideTestingMode` *Boolean*, default `False`
- `onRequest` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onResponse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)

### GetElement

```
GetElement(String cssSelector = null, Object parentElement = null, String position = null) : PLang.Modules.WebCrawlerModule.PlangWebElement
```

Gets one element from the dom by cssSelector, when cssSelector is null it uses that last element accessed. parentElement is either cssSelector or an PlangWebElement being references. position=null|first|last|idx|%variable%, idx is a number


### GetElementByText

```
GetElementByText(String text, String operatorOnText = equals, Nullable<Int32> timeoutInSeconds = null, String cssSelector = null) : PLang.Modules.WebCrawlerModule.PlangWebElement
```

operatorOnText can be equals|contains|startswith|endswith


### GetElements

```
GetElements(String cssSelector = null, Object parentElement = null) : List<PlangWebElement>
```

Get elements from page by cssSelector, when cssSelector is null it uses that last element accessed. parentElement is either cssSelector or an PlangWebElement being references


### GetElements

```
GetElements(String cssSelector = null, String shadowDomCssSelector = null) : PLang.Modules.WebCrawlerModule.PlangWebElement
```


### GetElementsInsideElement

```
GetElementsInsideElement(String elementName, Microsoft.Playwright.IElementHandle element = null) : List<PlangWebElement>
```


### GetPage

```
GetPage(Int32 idx = -1) : Microsoft.Playwright.IPage
```


### GetRequestHeaders

```
GetRequestHeaders(List<String> keys = null, String keyOperator = equals, String value = null, String valueOperator = contains) : Object
```

keys of the header, find by value where operation (startwith|endwith|equals|contains), request object can be null, will use the pages request


### GetResponseHeaders

```
GetResponseHeaders(List<String> keys = null, String keyOperator = equals, String value = null, String valueOperator = contains) : Object
```

key of the header, find by value where operation (startwith|endwith|equals|contains), request object can be null, will use the pages request


### GetUriFromPage

```
GetUriFromPage(Nullable<Int32> tabIndex = null) : Uri
```


### Input

```
Input(String value, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object
```

set the value of an input by cssSelector


### NavigateToUrl

```
NavigateToUrl(String url, String browserType = Chrome, Boolean headless = False, String profileName = , Boolean kioskMode = False, Dictionary<String, Object> argumentOptions = null, Nullable<Int32> timeoutInSeconds = null, Boolean hideTestingMode = False, Int32 pageIndex = -1, PLang.Models.GoalToCallInfo onRequest = null, PLang.Models.GoalToCallInfo onResponse = null, PLang.Models.GoalToCallInfo onWebsocketReceived = null, PLang.Models.GoalToCallInfo onWebsocketSent = null, PLang.Models.GoalToCallInfo onConsoleOutput = null, PLang.Models.GoalToCallInfo onWorker = null, PLang.Models.GoalToCallInfo onDialog = null, PLang.Models.GoalToCallInfo onLoad = null, PLang.Models.GoalToCallInfo onDOMLoad = null, PLang.Models.GoalToCallInfo onFileChooser = null, PLang.Models.GoalToCallInfo onIFrameLoad = null, PLang.Models.GoalToCallInfo onDownload = null) : object
```

opens a page to a url. browserType=Chrome|Edge|Firefox|IE|Safari. hideTestingMode tries to disguise that it is a bot.

- `url` *String*
- `browserType` *String*, default `Chrome`
- `headless` *Boolean*, default `False`
- `profileName` *String*, default ``
- `kioskMode` *Boolean*, default `False`
- `argumentOptions` *Dictionary<String, Object>*, default `null`
- `timeoutInSeconds` *Nullable<Int32>*, default `null`
- `hideTestingMode` *Boolean*, default `False`
- `pageIndex` *Int32*, default `-1`
- `onRequest` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onResponse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onWebsocketReceived` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onWebsocketSent` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onConsoleOutput` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onWorker` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onDialog` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onLoad` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onDOMLoad` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onFileChooser` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onIFrameLoad` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onDownload` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)

### ReloadPage

```
ReloadPage(Microsoft.Playwright.PageReloadOptions options = null) : object
```

- `options` *Microsoft.Playwright.PageReloadOptions*, default `null` — (see Type information in SupportingObjects)

### RemoveHeaderFromRequest

```
RemoveHeaderFromRequest(String url, List<String> headersToRemove = null) : object
```

url can be regex. when user does not define url match all with url=**/*


### ScrollToBottom

```
ScrollToBottom() : object
```


### ScrollToElement

```
ScrollToElement(PLang.Modules.WebCrawlerModule.PlangWebElement element) : object
```

- `element` *PLang.Modules.WebCrawlerModule.PlangWebElement* — (see Type information in SupportingObjects)

### ScrollToElementByCssSelector

```
ScrollToElementByCssSelector(String cssSelector) : object
```


### SelectByText

```
SelectByText(String text, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object
```

select an option by its text in select input by cssSelector


### SelectByValue

```
SelectByValue(String value, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object
```

select an option by its value in select input by cssSelector


### SendKey

```
SendKey(String value, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null, Boolean humanStyle = False) : object
```

Writes a text to an element


### SetFocus

```
SetFocus(String cssSelector = null, Nullable<Int32> timoutInSeconds = null) : object
```


### SetTextOnElement

```
SetTextOnElement(String text, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null, Boolean clearElementFirst = False) : object
```

set the text of an element other than input by cssSelector


### StartBrowser

```
StartBrowser(String browserType = Chrome, Boolean headless = False, String profileName = , Boolean kioskMode = False, Dictionary<String, Object> argumentOptions = null, Nullable<Int32> timoutInSeconds = null, Boolean hideTestingMode = False, PLang.Models.GoalToCallInfo onRequest = null, PLang.Models.GoalToCallInfo onResponse = null) : PLang.Modules.WebCrawlerModule.Models.BrowserInstance
```

browserType=Chrome|Edge|Firefox|Safari. hideTestingMode tries to disguise that it is a bot. when user want to use the default profile, set profileName="default"

- `browserType` *String*, default `Chrome`
- `headless` *Boolean*, default `False`
- `profileName` *String*, default ``
- `kioskMode` *Boolean*, default `False`
- `argumentOptions` *Dictionary<String, Object>*, default `null`
- `timoutInSeconds` *Nullable<Int32>*, default `null`
- `hideTestingMode` *Boolean*, default `False`
- `onRequest` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onResponse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)

### Submit

```
Submit(String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object
```


### SwitchTab

```
SwitchTab(Int32 tabIndex) : object
```


### TakeScreenshotOfWebsite

```
TakeScreenshotOfWebsite(String saveToPath, Boolean overwrite = False, String cssSelector = null) : object
```


### Wait

```
Wait(Int32 milliseconds = 1000) : object
```


### WaitForElementToAppear

```
WaitForElementToAppear(String cssSelector, Int32 timeoutInSeconds = 30, Boolean waitForElementToChange = False) : object
```


### WaitForUrl

```
WaitForUrl(String expectedUrl, Int32 timeoutInSeconds) : object
```


