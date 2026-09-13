# Module reference

Generated from the running code by `Tools/ModuleDocGen`, so it lists exactly the methods the
builder can map a step onto. Run it again after changing any module:

```
dotnet run --project Tools/ModuleDocGen
```

The hint in the second column is what you put in front of a step to force the module, e.g.
`- [file] write %content% to file.txt`. The hint is matched as a substring of the module name
(`StepBuilder.GetUserRequestedModule`), so any substring that hits one module works and
`[list]` selects ListDictionaryModule just as well as `[listdictionary]`. A hint that matches
several modules narrows the choice to those and leaves the last word to the llm, which is why
the column spells out the ambiguous ones.

| Module | Hint | Methods | What it does |
| --- | --- | --- | --- |
| [AiModule](./AiModule.md) | `[aimodule]` | 1 |  |
| [AppModule](./AppModule.md) | `[app] (ambiguous, matches AppModule, WindowAppModule)` | 1 | Call an App. When the user has the word 'app' in his statement, this should be called. |
| [AssertModule](./AssertModule.md) | `[assert]` | 2 | Assert object/variable/text or other entity to be what is epxected. For unit testing |
| [BlockchainModule](./BlockchainModule.md) | `[blockchain]` | 49 | Use blockchain, create wallet, account info, transfer money |
| [CachingModule](./CachingModule.md) | `[caching]` | 4 | Handles caching of objects, set, get and remove. object can be can be cached with sliding or fixed period |
| [CallGoalModule](./CallGoalModule.md) | `[callgoal]` | 1 | Call a Goal, e.g. `call goal Process`, `call Visits` |
| [CodeModule](./CodeModule.md) | `[code]` | 2 | Generate or Run existing c# code from user description. Only use if no other module is found or if [code] is defined. |
| [CompressionModule](./CompressionModule.md) | `[compression]` | 4 | compress and decompress(extract) a file or folder. This can be various of file formats, zip, gz, or other custom formats. Example usage: `zip file.txt to file.z... |
| [ConditionalModule](./ConditionalModule.md) | `[conditional]` | 20 | if statement for the user intent. Example 1:'if %isValid% is true then call SomeGoal, else call OtherGoal', this condition would return true if %isValid% is tru... |
| [ConvertModule](./ConvertModule.md) | `[convert]` | 4 | Convert object from one to another. html => md, md => html, string => keyvalue list |
| [CryptographicModule](./CryptographicModule.md) | `[cryptographic]` | 22 | Encrypt, descryption and hashing & verify using bcrypt, compute Sha256Hash, generate & validate Bearer token |
| [DbModule](./DbModule.md) | `[db]` | 34 | Database access, select, insert, update, delete and execute raw sql. Handles transactions. Sets and create datasources. Isolated data pattern (idp) |
| [DesktopModule](./DesktopModule.md) | `[desktop]` | 1 | Get information about what windows are open on the operating system |
| [EnvironmentModule](./EnvironmentModule.md) | `[environment]` | 25 | Information about the environment that the software is running, such as machine name, os user name, process id, settings, debug modes, set culture, open file in... |
| [EventModule](./EventModule.md) | `[event]` | 1 | Bind an event to app, goal, step, error, and any kind of custom errors. Example: - run before app starts, call goal Starting - run on each step, call StepAfter ... |
| [FileModule](./FileModule.md) | `[file]` | 36 | Handle file system access. Listen to files and dirs. Get permission to file and folder paths. Reads files, such as text, llm, csv, xls, pdf files and raw stream |
| [FilterModule](./FilterModule.md) | `[filter]` | 9 | Allow user to find text, filter out items, select, query from a %variable% and get specific item from that variable. ```plang - filter %list% where id=%id%, wri... |
| [HtmlModule](./HtmlModule.md) | `[html]` | 5 | Parse html text, extract from css selectors |
| [HttpModule](./HttpModule.md) | `[http]` | 12 | Make Http request. Mare sure to format Bearer authentication correctly in headers variable |
| [IdentityModule](./IdentityModule.md) | `[identity]` | 13 | Handles Identity in plang. Sign content and verifies signature with the identity. |
| [ImageModule](./ImageModule.md) | `[image]` | 1 | Image generation and manipulation including QR codes, barcodes, and image processing |
| [InjectModule](./InjectModule.md) | `[inject]` | 1 | Dependancy injection |
| [InstallModule](./InstallModule.md) | `[install]` | 1 | Install external utility from url |
| [ListDictionaryModule](./ListDictionaryModule.md) | `[listdictionary]` | 13 | get first\|last\|random\|position\| item from list or dictionary. Add, update, delete and retrieve list or dictionary. Group by key, merge two lists |
| [LlmModule](./LlmModule.md) | `[llmmodule]` | 8 | Ask LLM a question and recieve and answer |
| [LoggerModule](./LoggerModule.md) | `[logger]` | 1 |  |
| [LoopModule](./LoopModule.md) | `[loop]` | 2 | While, for, foreach, loops, repeat, go through a list and call a goal |
| [MathModule](./MathModule.md) | `[math]` | 3 | Solves math expressions |
| [MessageModule](./MessageModule.md) | `[message]` | 8 | Send and recieve private messages. Get account(public key), set current account for messaging |
| [MockModule](./MockModule.md) | `[mock]` | 1 | Mock other modules. Code should start with `mock XXX` where XXX would be the module, each mock needs to call a goal that will perform the mocking Example `mock ... |
| [OptionsModule](./OptionsModule.md) | `[options]` | 1 | Set options for the code. Only set the options that are defined by user |
| [OutputModule](./OutputModule.md) | `[output]` | 5 | Writes to the output stream. Ask a question with either text or template file. output stream can be to the user(default), system, to different channels such aud... |
| [PlangModule](./PlangModule.md) | `[plang]` | 25 | get apps available, compiles plang code, gets goals and steps from .goal files, method descriptions and class scheme. Runtime Engine for plang, can run goal and... |
| [PythonModule](./PythonModule.md) | `[python]` | 1 | Runs python scripts. Parameters can be passed to the python process |
| [ScheduleModule](./ScheduleModule.md) | `[schedule]` | 5 | Wait, Sleep and time delay. Cron scheduler |
| [SerializerModule](./SerializerModule.md) | `[serializer]` | 5 |  |
| [TemplateEngineModule](./TemplateEngineModule.md) | `[templateengine]` | 2 | Render template html, files, elements using template engine. plang examples:  ``` - render file.html - render %content% to #main / will render the variable into... |
| [TerminalModule](./TerminalModule.md) | `[terminal]` | 2 | Terminal/Console access to run external applications |
| [ThrowErrorModule](./ThrowErrorModule.md) | `[throwerror]` | 6 | Allows user to throw error or retry a step. Allows user to return out of goal or stop(end) running goal. Create payment request(status code 402) |
| [UdpModule](./UdpModule.md) | `[udp]` | 1 |  |
| [UiModule](./UiModule.md) | `[ui]` | 14 | Takes any user command and tries to convert it to html. Add, remove, insert content to css selector. Set the (default) layout for the UI. Execute javascript. |
| [ValidateModule](./ValidateModule.md) | `[validate]` | 9 | DO NOT USE for if statements. Validates a variable, make sure it's not empty, follows a pattern, is a number, etc. |
| [VariableModule](./VariableModule.md) | `[variable]` | 38 | Set, Get & Return variable(s). Set(NOT if statement) on variable includes condition such as empty or null, and a fallback written as `, default is X`, `, or X i... |
| [WebCrawlerModule](./WebCrawlerModule.md) | `[webcrawler]` | 38 | Run a browser instance, browse a website, input values and click on html elements, sendkeys, wait for browser and extract content |
| [WebserverModule](./WebserverModule.md) | `[webserver]` | 21 | Start webserver, add route, set certificate, read/write to Header, Cookie, send file to client |
| [WebSocketModule](./WebSocketModule.md) | `[websocket]` | 2 |  |
| [WindowAppModule](./WindowAppModule.md) | `[windowapp]` | 1 |  |
| [XmlModule](./XmlModule.md) | `[xml]` | 1 |  |

## Every method, alphabetically

| Method | Module | Signature |
| --- | --- | --- |
| AcceptAlert | [WebCrawlerModule](./WebCrawlerModule.md) | `AcceptAlert() : String` |
| AddDefaultRequestHeader | [WebCrawlerModule](./WebCrawlerModule.md) | `AddDefaultRequestHeader(Dictionary<String, Object> headers = null) : object` |
| AddElement | [XmlModule](./XmlModule.md) | `AddElement(Xml.XmlDocument xmlDoc, String nameOfElement, String insertElementInsideElement, Dictionary<String, String> attributeOnElement = null, Dictionary<String, Object> subElements = null) : Xml.XmlDocument` |
| AddItemsToDictionary | [ListDictionaryModule](./ListDictionaryModule.md) | `AddItemsToDictionary(String key, Dictionary<String, Object> value = null, Dictionary<String, Object> dictionaryInstance = null, Boolean updateIfExists = True) : String` |
| AddItemsToList | [ListDictionaryModule](./ListDictionaryModule.md) | `AddItemsToList(List<Object> value = null) : List<Object>` |
| AddPrivateKey | [CryptographicModule](./CryptographicModule.md) | `AddPrivateKey(String privateKey) : object` |
| AddRoute | [WebserverModule](./WebserverModule.md) | `AddRoute(String path, List<PLang.Modules.WebserverModule.Program+ParamInfo> pathParameters, PLang.Models.GoalToCallInfo goalToCall, PLang.Modules.WebserverModule.Program+RequestProperties requestProperties = null, PLang.Modules.WebserverModule.Program+ResponseProperties responseProperties = null) : object` |
| AddSerializer | [SerializerModule](./SerializerModule.md) | `AddSerializer(String path) : Nullable<Int32>` |
| AddToDictionary | [ListDictionaryModule](./ListDictionaryModule.md) | `AddToDictionary(String key, Object value, Dictionary<String, Object> dictionaryInstance = null, Boolean updateIfExists = True) : Dictionary<String,Object>` |
| AddToList | [ListDictionaryModule](./ListDictionaryModule.md) | `AddToList(Object value, List<Object> listInstance = null, Boolean uniqueValue = False, Boolean caseSensitive = False) : Object` |
| AddTypeMapping | [FileModule](./FileModule.md) | `AddTypeMapping(String extension, String type, String contentType = null) : object` |
| AllowanceFromSmartContract | [BlockchainModule](./BlockchainModule.md) | `AllowanceFromSmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger value) : Object` |
| AppendToAssistant | [LlmModule](./LlmModule.md) | `AppendToAssistant(String assistant) : object` |
| AppendToFile | [FileModule](./FileModule.md) | `AppendToFile(String path, String content, String seperator = null, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8) : object` |
| AppendToSystem | [LlmModule](./LlmModule.md) | `AppendToSystem(String system) : object` |
| AppendToUser | [LlmModule](./LlmModule.md) | `AppendToUser(String user) : object` |
| AppendToVariable | [VariableModule](./VariableModule.md) | `AppendToVariable(String key, Object value = null, Char seperator = 
, String valueLocation = postfix, String seperatorLocation = end, Boolean shouldBeUnique = False, Boolean doNotLoadVariablesInValue = False) : Object` |
| ApproveSmartContract | [BlockchainModule](./BlockchainModule.md) | `ApproveSmartContract(String contractAddressOrSymbol, String spender, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object` |
| ArchiveIdentity | [IdentityModule](./IdentityModule.md) | `ArchiveIdentity(String nameOrIdentity) : PLang.Interfaces.Identity` |
| Ask | [OutputModule](./OutputModule.md) | `Ask(PLang.Services.OutputStream.Messages.AskMessage askMessage) : Object` |
| AskLlm | [LlmModule](./LlmModule.md) | `AskLlm(List<PLang.Models.LlmMessage> promptMessages, String scheme = null, String model = gpt-4.1-mini, Double temperature = 0, Double topP = 0, Double frequencyPenalty = 0, Double presencePenalty = 0, Int32 maxLength = 4000, Boolean cacheResponse = True, String llmResponseType = null, Boolean continuePrevConversation = False, PLang.Modules.LlmModule.Program+Tools tools = null) : Object` |
| AssertPageContains | [WebCrawlerModule](./WebCrawlerModule.md) | `AssertPageContains(PLang.Modules.WebCrawlerModule.Program+AssertCheck assertCheck) : object` |
| BalanceOfBatchOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `BalanceOfBatchOnSmartContract(String contractAddressOrSymbol, String[] addresses, Numerics.BigInteger[] ids) : Object` |
| BalanceOfOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `BalanceOfOnSmartContract(String contractAddressOrSymbol, String addressToCheckBalanceOf) : Object` |
| BeginTransaction | [DbModule](./DbModule.md) | `BeginTransaction(List<String> dataSourceNames = null, PLang.Models.GoalToCallInfo onRollback = null) : object` |
| BindEvent | [EventModule](./EventModule.md) | `BindEvent(PLang.Events.EventBinding eventBinding) : object` |
| BuildPlangCode | [PlangModule](./PlangModule.md) | `BuildPlangCode(PLang.Building.Model.Goal goal) : object` |
| BuildPlangStep | [PlangModule](./PlangModule.md) | `BuildPlangStep(PLang.Building.Model.GoalStep step) : PLang.Building.Model.GoalStep` |
| BurnSmartContract | [BlockchainModule](./BlockchainModule.md) | `BurnSmartContract(String contractAddressOrSymbol, String account, Numerics.BigInteger amount, Boolean waitForReceipt = False) : Object` |
| CallAndSignFunction | [BlockchainModule](./BlockchainModule.md) | `CallAndSignFunction(String contractAddressOrSymbol, String abi, Object[] functionInputs = null, Boolean waitForReceipt = False) : Object` |
| CallFunction | [BlockchainModule](./BlockchainModule.md) | `CallFunction(String contractAddressOrSymbol, String abi, Object[] functionInputs = null) : Object` |
| CanSeeErrorDetails | [EnvironmentModule](./EnvironmentModule.md) | `CanSeeErrorDetails() : Boolean` |
| CecimalsOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `CecimalsOnSmartContract(String contractAddressOrSymbol) : Object` |
| ClearDictionary | [ListDictionaryModule](./ListDictionaryModule.md) | `ClearDictionary(Dictionary<String, Object> dictionary = null) : object` |
| ClearHtml | [HtmlModule](./HtmlModule.md) | `ClearHtml(String html) : String` |
| ClearList | [ListDictionaryModule](./ListDictionaryModule.md) | `ClearList(List<Object> listInstance = null) : object` |
| Click | [WebCrawlerModule](./WebCrawlerModule.md) | `Click(String cssSelector, Int32 elementAtToClick = 0, Boolean clickAllMatchingElements = False, Nullable<Int32> timeoutInSeconds = null) : object` |
| ClickOnElement | [WebCrawlerModule](./WebCrawlerModule.md) | `ClickOnElement(PLang.Modules.WebCrawlerModule.PlangWebElement element) : object` |
| CloseBrowser | [WebCrawlerModule](./WebCrawlerModule.md) | `CloseBrowser() : object` |
| CloseWindow | [UiModule](./UiModule.md) | `CloseWindow(PLang.Modules.UiModule.Program+DialogCommand dialogCommand) : object` |
| CompoundCondition | [ConditionalModule](./ConditionalModule.md) | `CompoundCondition(PLang.Modules.ConditionalModule.ConditionEvaluator+CompoundCondition condition, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| CompressDirectory | [CompressionModule](./CompressionModule.md) | `CompressDirectory(String sourceDirectoryName, String destinationArchiveFileName, Int32 compressionLevel = 0, Boolean includeBaseDirectory = True, Boolean createDestinationDirectory = True, Boolean overwriteDestinationFile = False, String[] excludePatterns = null) : object` |
| CompressFile | [CompressionModule](./CompressionModule.md) | `CompressFile(String filePath, String saveToPath, Int32 compressionLevel = 0, Boolean overwrite = False) : object` |
| CompressFiles | [CompressionModule](./CompressionModule.md) | `CompressFiles(String[] filePaths, String saveToPath, Int32 compressionLevel = 0, Boolean overwrite = False) : object` |
| Connect | [WebSocketModule](./WebSocketModule.md) | `Connect(String url, String name = null, Dictionary<String, Object> headers = null, PLang.Models.GoalToCallInfo onMessage = null, PLang.Models.GoalToCallInfo onConnected = null, PLang.Models.GoalToCallInfo onClose = null, PLang.Models.GoalToCallInfo onError = null, Int32 bufferSize = 8192) : Object` |
| Contains | [AssertModule](./AssertModule.md) | `Contains(Object contains, Object actualValue) : object` |
| ContainsNumbers | [ConditionalModule](./ConditionalModule.md) | `ContainsNumbers(Object item, List<Int32> contains = null, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| ContainsString | [ConditionalModule](./ConditionalModule.md) | `ContainsString(Object item, String contains, Boolean isNot = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| ConvertFromBase64 | [CryptographicModule](./CryptographicModule.md) | `ConvertFromBase64(String base64) : Byte[]` |
| ConvertHtmlToPdf | [ConvertModule](./ConvertModule.md) | `ConvertHtmlToPdf(PLang.Modules.ConvertModule.Program+ConvertHtmlToPdfInstruction options) : object` |
| ConvertMdToHtml | [ConvertModule](./ConvertModule.md) | `ConvertMdToHtml(String content, Boolean useAdvancedExtension = True, List<String> markdownPipelineExtensions = null) : String` |
| ConvertToBase64 | [CryptographicModule](./CryptographicModule.md) | `ConvertToBase64(String content) : String` |
| ConvertToBase64 | [VariableModule](./VariableModule.md) | `ConvertToBase64(String key) : String` |
| ConvertToKeyValueList | [ConvertModule](./ConvertModule.md) | `ConvertToKeyValueList(Object variable, String newLineSeperator = 
, String columnSeperator = 	, Boolean trimColumns = True, List<String> headers = null) : Object` |
| ConvertToMd | [ConvertModule](./ConvertModule.md) | `ConvertToMd(Object content, String unknownTags = Bypass, Boolean githubFlavored = True, Boolean removeComments = True, Boolean smartHrefHandling = True, Boolean cleanupUnnecessarySpaces = True, Boolean suppressDivNewlines = True) : String` |
| ConvertToType | [SerializerModule](./SerializerModule.md) | `ConvertToType(PLang.Models.ObjectValue<List<String>> variables, String type) : Object` |
| CopyFile | [FileModule](./FileModule.md) | `CopyFile(String sourceFileName, String destFileName, Boolean createDirectoryIfNotExisting = False, Boolean overwriteFile = False) : object` |
| CopyFiles | [FileModule](./FileModule.md) | `CopyFiles(String directoryPath, String destinationPath, String searchPattern = *, String[] excludePatterns = null, Boolean includeSubfoldersAndFiles = False, Boolean overwriteFiles = False) : object` |
| CreateDataSource | [DbModule](./DbModule.md) | `CreateDataSource(String name = data, String databaseType = sqlite, Nullable<Boolean> setAsDefaultForApp = null, Nullable<Boolean> keepHistoryEventSourcing = null) : PLang.Modules.DbModule.ModuleSettings+DataSource` |
| CreateDirectory | [FileModule](./FileModule.md) | `CreateDirectory(String directoryPath, Boolean incrementalNaming = False) : String` |
| CreateIdentity | [IdentityModule](./IdentityModule.md) | `CreateIdentity(String name, Boolean setAsDefault = False) : PLang.Interfaces.Identity` |
| CreatePathByJoining | [FileModule](./FileModule.md) | `CreatePathByJoining(String[] paths) : String` |
| CreatePaymentRequest | [ThrowErrorModule](./ThrowErrorModule.md) | `CreatePaymentRequest(String name, String description, String error, List<Dictionary<String, Object>> services = null) : PLang.Errors.Types.PaymentContract` |
| CreateSalt | [CryptographicModule](./CryptographicModule.md) | `CreateSalt(Int32 workFactor = 12) : String` |
| CreateTable | [DbModule](./DbModule.md) | `CreateTable(String sql) : Int64` |
| CreateToken | [CryptographicModule](./CryptographicModule.md) | `CreateToken(String password = null, Int32 validForSeconds = 600, String secretKeyName = null) : String` |
| DecompressFile | [CompressionModule](./CompressionModule.md) | `DecompressFile(String sourceArchiveFileName, String destinationDirectoryName, Boolean overwrite = False) : object` |
| Decrypt | [CryptographicModule](./CryptographicModule.md) | `Decrypt(String content) : Object` |
| Delete | [DbModule](./DbModule.md) | `Delete(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64` |
| Delete | [HttpModule](./HttpModule.md) | `Delete(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| DeleteCookie | [WebserverModule](./WebserverModule.md) | `DeleteCookie(String name) : object` |
| DeleteDirectory | [FileModule](./FileModule.md) | `DeleteDirectory(String directoryPath, Boolean recursive = True, Boolean throwErrorOnNotFound = False) : object` |
| DeleteFile | [FileModule](./FileModule.md) | `DeleteFile(String fileName, Boolean throwErrorOnNotFound = False) : object` |
| DeleteFromList | [ListDictionaryModule](./ListDictionaryModule.md) | `DeleteFromList(Object item, List<Object> listInstance = null) : Boolean` |
| DeleteKeyFromDictionary | [ListDictionaryModule](./ListDictionaryModule.md) | `DeleteKeyFromDictionary(String key, Dictionary<String, Object> dictionary = null) : Boolean` |
| Deserialize | [SerializerModule](./SerializerModule.md) | `Deserialize(IO.Stream stream, String serializer = json) : Object` |
| Deserialize | [SerializerModule](./SerializerModule.md) | `Deserialize(Byte[] data, String serializer = json) : Object` |
| DirectoryExists | [ConditionalModule](./ConditionalModule.md) | `DirectoryExists(String dirPathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| DownloadFile | [HttpModule](./HttpModule.md) | `DownloadFile(String url, String pathToSaveTo, Boolean overwriteFile = False, Dictionary<String, Object> headers = null, Boolean createPathToSaveTo = True, Boolean doNotDownloadIfFileExists = False) : String` |
| Encrypt | [CryptographicModule](./CryptographicModule.md) | `Encrypt(Object content) : String` |
| EndApp | [EnvironmentModule](./EnvironmentModule.md) | `EndApp() : object` |
| EndApp | [ThrowErrorModule](./ThrowErrorModule.md) | `EndApp() : object` |
| EndGoalExecution | [ThrowErrorModule](./ThrowErrorModule.md) | `EndGoalExecution(String message = null, Int32 levels = 0) : object` |
| EndTransaction | [DbModule](./DbModule.md) | `EndTransaction() : object` |
| Execute | [DbModule](./DbModule.md) | `Execute(String dataSourceName, String sql, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64` |
| ExecuteByConnectionString | [DbModule](./DbModule.md) | `ExecuteByConnectionString(String sql, String connectionString, String dbType = sqlite, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64` |
| ExecuteDynamicSql | [DbModule](./DbModule.md) | `ExecuteDynamicSql(String dataSourceName, String sql, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64` |
| ExecuteJavascript | [UiModule](./UiModule.md) | `ExecuteJavascript(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object` |
| ExecutePlang | [DbModule](./DbModule.md) | `ExecutePlang(String sql, List<PLang.Runtime.ObjectValue> parameters = null) : Int64` |
| ExecuteSqlByConnectionString | [DbModule](./DbModule.md) | `ExecuteSqlByConnectionString(String pathToSql, String sql, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64` |
| ExecuteSqlFile | [DbModule](./DbModule.md) | `ExecuteSqlFile(String dataSourceName, String fileName, List<String> tableAllowList = null) : Int64` |
| Extract | [HtmlModule](./HtmlModule.md) | `Extract(String html, String cssSelector) : PLang.Runtime.ObjectValue` |
| ExtractByCssSelector | [FilterModule](./FilterModule.md) | `ExtractByCssSelector(String html, String cssSelector, String retrieveOneItem = null) : Object` |
| ExtractClassesToList | [WebCrawlerModule](./WebCrawlerModule.md) | `ExtractClassesToList(String[] cssSelectors, String fromCssSelector) : Object` |
| ExtractContent | [WebCrawlerModule](./WebCrawlerModule.md) | `ExtractContent(String cssSelector = null, PLang.Modules.WebCrawlerModule.PlangWebElement element = null, String outputFormat = html) : String` |
| ExtractElementsFromHtml | [FilterModule](./FilterModule.md) | `ExtractElementsFromHtml(String html, List<String> elementNames = null) : PLang.Modules.FilterModule.Program+HtmlNode` |
| ExtractForm | [HtmlModule](./HtmlModule.md) | `ExtractForm(PLang.Modules.HtmlModule.Program+ExtractFormParameters parameters) : Object` |
| ExtractFromXPath | [FilterModule](./FilterModule.md) | `ExtractFromXPath(String html, String xpath) : Object` |
| ExtractMarkdownWrapping | [FilterModule](./FilterModule.md) | `ExtractMarkdownWrapping(String input, String[] format = null) : Object` |
| ExtractSelect | [HtmlModule](./HtmlModule.md) | `ExtractSelect(PLang.Modules.HtmlModule.Program+ExtractSelectParameters parameters) : Object` |
| ExtractTable | [HtmlModule](./HtmlModule.md) | `ExtractTable(PLang.Modules.HtmlModule.Program+ExtractTableParameters parameters) : Object` |
| Fibonacci | [MathModule](./MathModule.md) | `Fibonacci(Int32 variable) : Nullable<Int32>` |
| FileExists | [ConditionalModule](./ConditionalModule.md) | `FileExists(String filePathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| FileExists | [FileModule](./FileModule.md) | `FileExists(String filePathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| FilterOnProperty | [FilterModule](./FilterModule.md) | `FilterOnProperty(Object variableToExtractFrom, String propertyToFilterOn, String operatorOnPropertyToFilterOn = =, String retrieveOneItem = null, String operatorToFilterOnValueComparer = insensitive, Boolean throwErrorOnEmptyResult = False, Object defaultValue = null) : Object` |
| FilterOnPropertyAndValue | [FilterModule](./FilterModule.md) | `FilterOnPropertyAndValue(PLang.Runtime.ObjectValue variableToExtractFrom, String propertyToFilterOn, Object valueToFilterBy, String operatorToFilterOnValue = =, String operatorOnPropertyToFilter = =, String propertyToExtract = null, String retrieveOneItem = null, String operatorToFilterOnValueComparer = insensitive, Boolean throwErrorOnEmptyResult = False, Object defaultValue = null) : Object` |
| FindElementAndExtractAttribute | [WebCrawlerModule](./WebCrawlerModule.md) | `FindElementAndExtractAttribute(String attribute, String cssSelector = null, PLang.Modules.WebCrawlerModule.PlangWebElement element = null) : String` |
| FindTextInContent | [FilterModule](./FilterModule.md) | `FindTextInContent(String content, String textToFind, String matching = contains, String retrieveOneItem = null) : Object` |
| Flush | [UiModule](./UiModule.md) | `Flush() : object` |
| GenerateBearerToken | [CryptographicModule](./CryptographicModule.md) | `GenerateBearerToken(String uniqueString, String issuer = PLangRuntime, String audience = user, Int32 expireTimeInSeconds = 604800) : String` |
| GenerateQrCode | [ImageModule](./ImageModule.md) | `GenerateQrCode(PLang.Modules.ImageModule.QrCode.QrCodeRequest request) : PLang.Modules.ImageModule.QrCode.QrCodeResult` |
| Get | [CachingModule](./CachingModule.md) | `Get(String key) : Object` |
| Get | [HttpModule](./HttpModule.md) | `Get(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| Get | [UdpModule](./UdpModule.md) | `Get(String key) : Object` |
| GetAdditionalAssistantErrorInfo | [DbModule](./DbModule.md) | `GetAdditionalAssistantErrorInfo() : String` |
| GetAdditionalSystemErrorInfo | [DbModule](./DbModule.md) | `GetAdditionalSystemErrorInfo() : String` |
| GetAppName | [EnvironmentModule](./EnvironmentModule.md) | `GetAppName() : String` |
| GetApprovedOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `GetApprovedOnSmartContract(String contractAddressOrSymbol) : Object` |
| GetBearerSecret | [CryptographicModule](./CryptographicModule.md) | `GetBearerSecret() : String` |
| GetBrowserInstance | [WebCrawlerModule](./WebCrawlerModule.md) | `GetBrowserInstance(String browserType = Chrome, Boolean headless = False, String profileName = , Boolean kioskMode = False, Dictionary<String, Object> argumentOptions = null, Nullable<Int32> timoutInSeconds = null, Boolean hideTestingMode = False, PLang.Models.GoalToCallInfo onRequest = null, PLang.Models.GoalToCallInfo onResponse = null) : PLang.Modules.WebCrawlerModule.Models.BrowserInstance` |
| GetClassDescription | [PlangModule](./PlangModule.md) | `GetClassDescription(String moduleName) : PLang.Building.Model.ClassDescription` |
| GetCookie | [WebserverModule](./WebserverModule.md) | `GetCookie(String name) : Object` |
| GetCookieRaw | [WebserverModule](./WebserverModule.md) | `GetCookieRaw(String name) : String` |
| GetCurrentAddress | [BlockchainModule](./BlockchainModule.md) | `GetCurrentAddress() : String` |
| GetCurrentCulture | [EnvironmentModule](./EnvironmentModule.md) | `GetCurrentCulture() : Globalization.CultureInfo` |
| GetCurrentFolderPath | [FileModule](./FileModule.md) | `GetCurrentFolderPath(String path) : String` |
| GetCurrentRpcServer | [BlockchainModule](./BlockchainModule.md) | `GetCurrentRpcServer() : PLang.Modules.BlockchainModule.ModuleSettings+RpcServer` |
| GetDatabaseStructure | [DbModule](./DbModule.md) | `GetDatabaseStructure(String dataSourceName, List<String> tables = null) : IEnumerable<TableInfo>` |
| GetDataSource | [DbModule](./DbModule.md) | `GetDataSource(String name = null) : PLang.Modules.DbModule.ModuleSettings+DataSource` |
| GetDataSources | [DbModule](./DbModule.md) | `GetDataSources() : PLang.Modules.DbModule.ModuleSettings+DataSource` |
| GetDbScheme | [DbModule](./DbModule.md) | `GetDbScheme(String dataSourceName) : List<String>` |
| GetDecimal | [BlockchainModule](./BlockchainModule.md) | `GetDecimal(String contractAddress) : UInt32` |
| GetDirectoryPathsInDirectory | [FileModule](./FileModule.md) | `GetDirectoryPathsInDirectory(String directoryPath = ./, String regexSearchPattern = null, String[] excludePatterns = null, Boolean includeSubfolders = False, Boolean includeSystemFolder = False, Boolean includeDirectoryInfo = False) : PLang.Modules.FileModule.Program+Directory` |
| GetElement | [WebCrawlerModule](./WebCrawlerModule.md) | `GetElement(String cssSelector = null, Object parentElement = null, String position = null) : PLang.Modules.WebCrawlerModule.PlangWebElement` |
| GetElementByText | [WebCrawlerModule](./WebCrawlerModule.md) | `GetElementByText(String text, String operatorOnText = equals, Nullable<Int32> timeoutInSeconds = null, String cssSelector = null) : PLang.Modules.WebCrawlerModule.PlangWebElement` |
| GetElements | [WebCrawlerModule](./WebCrawlerModule.md) | `GetElements(String cssSelector = null, Object parentElement = null) : List<PlangWebElement>` |
| GetElements | [WebCrawlerModule](./WebCrawlerModule.md) | `GetElements(String cssSelector = null, String shadowDomCssSelector = null) : PLang.Modules.WebCrawlerModule.PlangWebElement` |
| GetElementsInsideElement | [WebCrawlerModule](./WebCrawlerModule.md) | `GetElementsInsideElement(String elementName, Microsoft.Playwright.IElementHandle element = null) : List<PlangWebElement>` |
| GetEnvironmentVariable | [EnvironmentModule](./EnvironmentModule.md) | `GetEnvironmentVariable(String key) : String` |
| GetEnvironmentVariable | [VariableModule](./VariableModule.md) | `GetEnvironmentVariable(String key) : String` |
| GetFileInfo | [FileModule](./FileModule.md) | `GetFileInfo(String fileName) : PLang.Modules.FileModule.FileInfo` |
| GetFilePathsInDirectory | [FileModule](./FileModule.md) | `GetFilePathsInDirectory(String directoryPath = ./, String searchPattern = *, String[] excludePatterns = null, Boolean includeSubfolders = False, Boolean includeFileInfo = False, String filterOnType = null) : PLang.Modules.FileModule.Program+File` |
| GetFileType | [FileModule](./FileModule.md) | `GetFileType(String extension) : String` |
| GetFromDictionary | [ListDictionaryModule](./ListDictionaryModule.md) | `GetFromDictionary(String key, Dictionary<String, Object> dictionaryInstance = null) : Object` |
| GetFromList | [ListDictionaryModule](./ListDictionaryModule.md) | `GetFromList(Int32 position, List<Object> listInstance = null) : Object` |
| GetGoals | [PlangModule](./PlangModule.md) | `GetGoals(String fileOrFolderPath, String visibility = public, List<String> propertiesToExtract = null, String parser = pr) : Object` |
| GetHashOfFile | [CryptographicModule](./CryptographicModule.md) | `GetHashOfFile(String filePath, String hashAlgorithm = sha256, String encoding = base64) : String` |
| GetIdentities | [IdentityModule](./IdentityModule.md) | `GetIdentities() : PLang.Interfaces.Identity` |
| GetIdentity | [IdentityModule](./IdentityModule.md) | `GetIdentity(String nameOrIdentity) : PLang.Interfaces.Identity` |
| GetItem | [FilterModule](./FilterModule.md) | `GetItem(Object variableToExtractFrom, String retrieveOneItem) : Object` |
| GetItem | [ListDictionaryModule](./ListDictionaryModule.md) | `GetItem(String operator = first, List<Object> listInstance = null, List<String> sortColumns = null, List<String> sortOperator = null) : Object` |
| GetLlmIdentity | [LlmModule](./LlmModule.md) | `GetLlmIdentity() : String` |
| GetMachineName | [EnvironmentModule](./EnvironmentModule.md) | `GetMachineName() : String` |
| GetMehodInfo | [PlangModule](./PlangModule.md) | `GetMehodInfo(String type, String methodName) : PLang.Building.Model.ClassDescription` |
| GetMemoryInfo | [EnvironmentModule](./EnvironmentModule.md) | `GetMemoryInfo(Boolean forceGarbageCollection = False) : PLang.Modules.EnvironmentModule.Program+MemoryInfo` |
| GetMethodMappingScheme | [PlangModule](./PlangModule.md) | `GetMethodMappingScheme() : String` |
| GetMethods | [PlangModule](./PlangModule.md) | `GetMethods(List<String> modules = null, String format = null) : Object` |
| GetMocks | [EnvironmentModule](./EnvironmentModule.md) | `GetMocks() : PLang.Modules.MockModule.Program+MockData` |
| GetModules | [PlangModule](./PlangModule.md) | `GetModules(String stepText = null, List<String> excludeModules = null) : String` |
| GetModules2 | [PlangModule](./PlangModule.md) | `GetModules2(String format = null) : Object` |
| GetMyBalanceOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `GetMyBalanceOnSmartContract(String contractAddressOrSymbol) : Object` |
| GetMyIdentity | [IdentityModule](./IdentityModule.md) | `GetMyIdentity() : PLang.Interfaces.Identity` |
| GetNativeBalanceOfAddressInWei | [BlockchainModule](./BlockchainModule.md) | `GetNativeBalanceOfAddressInWei(String address) : Numerics.BigInteger` |
| GetNativeBalanceOfAddressToDecimalPoint | [BlockchainModule](./BlockchainModule.md) | `GetNativeBalanceOfAddressToDecimalPoint(String address, Int32 decimalPlacesToUnit = 18) : Decimal` |
| GetNumberOfLiveConnections | [WebserverModule](./WebserverModule.md) | `GetNumberOfLiveConnections(Int32 lastUpdatedInSeconds = 0) : Int64` |
| GetOpenWindows | [DesktopModule](./DesktopModule.md) | `GetOpenWindows() : PLang.Modules.DesktopModule.Program+WindowInfo` |
| GetOrCreateWallet | [BlockchainModule](./BlockchainModule.md) | `GetOrCreateWallet() : Nethereum.HdWallet.Wallet` |
| GetOSDescription | [EnvironmentModule](./EnvironmentModule.md) | `GetOSDescription() : String` |
| GetPage | [WebCrawlerModule](./WebCrawlerModule.md) | `GetPage(Int32 idx = -1) : Microsoft.Playwright.IPage` |
| GetPreviousMessages | [LlmModule](./LlmModule.md) | `GetPreviousMessages() : PLang.Models.LlmMessage` |
| GetPrivateKey | [BlockchainModule](./BlockchainModule.md) | `GetPrivateKey() : String` |
| GetPrivateKey | [CryptographicModule](./CryptographicModule.md) | `GetPrivateKey() : String` |
| GetPrivateKey | [IdentityModule](./IdentityModule.md) | `GetPrivateKey() : String` |
| GetPrivateKey | [MessageModule](./MessageModule.md) | `GetPrivateKey() : String` |
| GetPrivateKeyHash | [CryptographicModule](./CryptographicModule.md) | `GetPrivateKeyHash() : String` |
| GetProcessId | [EnvironmentModule](./EnvironmentModule.md) | `GetProcessId() : Int32` |
| GetPublicKey | [MessageModule](./MessageModule.md) | `GetPublicKey() : String` |
| GetRelays | [MessageModule](./MessageModule.md) | `GetRelays() : String` |
| GetRequestHeader | [WebserverModule](./WebserverModule.md) | `GetRequestHeader(String key) : String` |
| GetRequestHeaders | [WebCrawlerModule](./WebCrawlerModule.md) | `GetRequestHeaders(List<String> keys = null, String keyOperator = equals, String value = null, String valueOperator = contains) : Object` |
| GetResponseHeaders | [WebCrawlerModule](./WebCrawlerModule.md) | `GetResponseHeaders(List<String> keys = null, String keyOperator = equals, String value = null, String valueOperator = contains) : Object` |
| GetRpcServers | [BlockchainModule](./BlockchainModule.md) | `GetRpcServers() : PLang.Modules.BlockchainModule.ModuleSettings+RpcServer` |
| GetSettings | [EnvironmentModule](./EnvironmentModule.md) | `GetSettings() : PLang.Models.Setting` |
| GetSettings | [VariableModule](./VariableModule.md) | `GetSettings(String key) : object` |
| GetSetupGoals | [PlangModule](./PlangModule.md) | `GetSetupGoals(String appPath = null, String visibility = public, List<String> propertiesToExtract = null) : List<Goal>` |
| GetStep | [PlangModule](./PlangModule.md) | `GetStep(String goalPrPath, String stepPrFile) : PLang.Building.Model.GoalStep` |
| GetStepProperties | [PlangModule](./PlangModule.md) | `GetStepProperties(String moduleName, String methodName) : Dictionary<String,Object>` |
| GetSteps | [PlangModule](./PlangModule.md) | `GetSteps(String goalPath) : IReadOnlyList<GoalStep>` |
| GetTimeSpanRelated | [VariableModule](./VariableModule.md) | `GetTimeSpanRelated(DateTime fromDate, DateTime toDate, String pattern) : Object` |
| GetUriFromPage | [WebCrawlerModule](./WebCrawlerModule.md) | `GetUriFromPage(Nullable<Int32> tabIndex = null) : Uri` |
| GetUriOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `GetUriOnSmartContract(String contractAddressOrSymbol, Numerics.BigInteger id) : Object` |
| GetUserIp | [WebserverModule](./WebserverModule.md) | `GetUserIp(String headerKey = null) : String` |
| GetUserName | [EnvironmentModule](./EnvironmentModule.md) | `GetUserName() : String` |
| GetVariable | [VariableModule](./VariableModule.md) | `GetVariable(String key) : Object` |
| GetVariables | [PlangModule](./PlangModule.md) | `GetVariables(PLang.Building.Model.GoalStep step) : PLang.Runtime.ObjectValue` |
| GetWallets | [BlockchainModule](./BlockchainModule.md) | `GetWallets() : PLang.Modules.BlockchainModule.ModuleSettings+Wallet` |
| GiveAccess | [FileModule](./FileModule.md) | `GiveAccess(String path) : object` |
| GroupBy | [ListDictionaryModule](./ListDictionaryModule.md) | `GroupBy(Object obj, String key) : Object` |
| HasAccessToPath | [ConditionalModule](./ConditionalModule.md) | `HasAccessToPath(String dirOrFilePathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| Hash | [CryptographicModule](./CryptographicModule.md) | `Hash(Object variable, Nullable<Boolean> useSalt = null, String salt = null, String type = keccak256) : Object` |
| HashHmacShaInput | [CryptographicModule](./CryptographicModule.md) | `HashHmacShaInput(String input, String secretKey = null, Int32 hashSize = 256) : String` |
| HashIdentityString | [CryptographicModule](./CryptographicModule.md) | `HashIdentityString(String identity) : String` |
| HashInput | [CryptographicModule](./CryptographicModule.md) | `HashInput(Object variable, Boolean useSalt = True, String salt = null, String hashAlgorithm = keccak256) : String` |
| HashPassword | [CryptographicModule](./CryptographicModule.md) | `HashPassword(Object variable, Boolean returnAsString = False, Boolean useSalt = True, String salt = null, String type = keccak256) : Object` |
| HasPattern | [ValidateModule](./ValidateModule.md) | `HasPattern(String[] variables, String pattern, String errorMessage, Int32 statusCode = 400) : object` |
| Head | [HttpModule](./HttpModule.md) | `Head(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| HideErrorDetails | [EnvironmentModule](./EnvironmentModule.md) | `HideErrorDetails() : object` |
| Inject | [InjectModule](./InjectModule.md) | `Inject(String type, String pathToDll, Boolean isDefaultOrGlobalForWholeApp = False, String environmentVariable = PLANG_ENV, String environmentVariableValue = null) : object` |
| Input | [WebCrawlerModule](./WebCrawlerModule.md) | `Input(String value, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object` |
| Insert | [DbModule](./DbModule.md) | `Insert(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64` |
| InsertAndSelectIdOfInsertedRow | [DbModule](./DbModule.md) | `InsertAndSelectIdOfInsertedRow(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Object` |
| InsertBulk | [DbModule](./DbModule.md) | `InsertBulk(String dataSourceName, String tableName, List<Object> itemsToInsert = null, Dictionary<String, Object> columnMapping = null, Boolean ignoreContraintOnInsert = False) : Int64` |
| InsertOrUpdate | [DbModule](./DbModule.md) | `InsertOrUpdate(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64` |
| InsertOrUpdateAndSelectIdOfRow | [DbModule](./DbModule.md) | `InsertOrUpdateAndSelectIdOfRow(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : Object` |
| InstallFromUrl | [InstallModule](./InstallModule.md) | `InstallFromUrl(PLang.Modules.HttpModule.Program+HttpRequest request) : object` |
| InstallNpm | [EnvironmentModule](./EnvironmentModule.md) | `InstallNpm(String packageName) : object` |
| IsApprovedForAllOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `IsApprovedForAllOnSmartContract(String contractAddressOrSymbol, String accountAddress, String operatorAddress) : Object` |
| IsBase64 | [ConditionalModule](./ConditionalModule.md) | `IsBase64(String content, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsEmpty | [ConditionalModule](./ConditionalModule.md) | `IsEmpty(Object item, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsEqual | [AssertModule](./AssertModule.md) | `IsEqual(Object expectedValue, Object actualValue, String resultVariable = assertResult, String expectedValueType = null, String actualValueType = null) : object` |
| IsEqual | [ConditionalModule](./ConditionalModule.md) | `IsEqual(Object item1, Object item2, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, Boolean ignoreCase = True, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsFalse | [ConditionalModule](./ConditionalModule.md) | `IsFalse(Nullable<Boolean> item = null, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsInCSharpDebugMode | [EnvironmentModule](./EnvironmentModule.md) | `IsInCSharpDebugMode() : Boolean` |
| IsInDebugMode | [EnvironmentModule](./EnvironmentModule.md) | `IsInDebugMode() : Boolean` |
| IsLength | [ValidateModule](./ValidateModule.md) | `IsLength(String variableName, Int32 length, String compairer, String errorMessage, Int32 statusCode = 400) : object` |
| IsMod | [ConditionalModule](./ConditionalModule.md) | `IsMod(Double leftValue, Double modValue, Double equalsValue, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsNotEmpty | [ConditionalModule](./ConditionalModule.md) | `IsNotEmpty(Object item, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsNotEmpty | [ValidateModule](./ValidateModule.md) | `IsNotEmpty(List<PLang.Runtime.ObjectValue> variables, PLang.Services.OutputStream.Messages.ErrorMessage errorMessage) : object` |
| IsNotEmpty | [ValidateModule](./ValidateModule.md) | `IsNotEmpty(List<PLang.Runtime.ObjectValue> variables, String errorMessage, Int32 statusCode = 400) : object` |
| IsNotEqual | [ConditionalModule](./ConditionalModule.md) | `IsNotEqual(Object item1, Object item2, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, Boolean ignoreCase = True, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsOsPath | [ConditionalModule](./ConditionalModule.md) | `IsOsPath(String path, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsSystemPath | [ConditionalModule](./ConditionalModule.md) | `IsSystemPath(String path, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsTrue | [ConditionalModule](./ConditionalModule.md) | `IsTrue(Nullable<Boolean> item = null, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| IsValid2LetterCountryCode | [ValidateModule](./ValidateModule.md) | `IsValid2LetterCountryCode(String[] variables, String pattern, String errorMessage, Int32 statusCode = 400) : object` |
| Join | [FilterModule](./FilterModule.md) | `Join(List<Object> list = null, String seperator = , , String[] exclude = null) : String` |
| KeepAlive | [EnvironmentModule](./EnvironmentModule.md) | `KeepAlive(String message = App KeepAlive) : object` |
| Listen | [MessageModule](./MessageModule.md) | `Listen(PLang.Models.GoalToCallInfo goalName, String contentVariableName = content, String senderVariableName = sender, String eventVariableName = __NosrtEventKey__, Nullable<DateTimeOffset> listenFromDateTime = null, String[] onlyMessageFromSenders = null) : object` |
| ListenToApprovalEventOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `ListenToApprovalEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object` |
| ListenToApprovalForAllEventOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `ListenToApprovalForAllEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object` |
| ListenToBlock | [BlockchainModule](./BlockchainModule.md) | `ListenToBlock(PLang.Models.GoalToCallInfo callGoal, String subcriptionId = subscriptionId, PLang.Models.GoalToCallInfo callGoalOnUnsubscribe = null) : object` |
| ListenToEventOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `ListenToEventOnSmartContract(String contractAddressOrSymbol, String abi, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object` |
| ListenToFileChange | [FileModule](./FileModule.md) | `ListenToFileChange(List<String> fileSearchPatterns = null, PLang.Models.GoalToCallInfo goalToCall, List<String> excludeFiles = null, Boolean includeSubdirectories = False, Int64 debounceTime = 150, Boolean listenForFileChange = False, Boolean listenForFileCreated = False, Boolean listenForFileDeleted = False, Boolean listenForFileRename = False, String absoluteFilePathVariableName = FullPath, String fileNameVariableName = Name, String changeTypeVariableName = ChangeType, String senderVariableName = Sender, String oldFileAbsoluteFilePathVariableName = OldFullPath, String oldFileNameVariableName = OldName) : object` |
| ListenToTransferBatchEventOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `ListenToTransferBatchEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object` |
| ListenToTransferEventOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `ListenToTransferEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object` |
| ListenToTransferSingleEventOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `ListenToTransferSingleEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object` |
| ListenToUriEventOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `ListenToUriEventOnSmartContract(String contractAddressOrSymbol, PLang.Models.GoalToCallInfo goalToCall, String subscriptIdVariableName = subscriptionId) : object` |
| ListModules | [PlangModule](./PlangModule.md) | `ListModules() : List<ModuleInfo>` |
| Load | [VariableModule](./VariableModule.md) | `Load(List<String> variables = null, String dataSourceName = null) : Object` |
| LoadExtension | [DbModule](./DbModule.md) | `LoadExtension(String dataSourceName, String fileName, String procName = null) : object` |
| LoadVariables | [VariableModule](./VariableModule.md) | `LoadVariables(String key) : Object` |
| LoadWithDefaultValue | [VariableModule](./VariableModule.md) | `LoadWithDefaultValue(Dictionary<String, Object> variablesWithDefaultValue = null, String dataSourceName = null) : Object` |
| Log | [LoggerModule](./LoggerModule.md) | `Log(String message, String loggerLevel = information, Object[] args = null) : object` |
| MergeLists | [ListDictionaryModule](./ListDictionaryModule.md) | `MergeLists(Object list1, Object list2, String key) : Object` |
| MintSmartContract | [BlockchainModule](./BlockchainModule.md) | `MintSmartContract(String contractAddressOrSymbol, String to, Numerics.BigInteger amount, Boolean waitForReceipt = False) : Object` |
| MockMethod | [MockModule](./MockModule.md) | `MockMethod(PLang.Modules.MockModule.Program+MockData mockData) : object` |
| ModifyDateTime | [VariableModule](./VariableModule.md) | `ModifyDateTime(PLang.Modules.VariableModule.Program+ModifyDateTimeParameters p) : Object` |
| MoveFile | [FileModule](./FileModule.md) | `MoveFile(String sourceFileName, String destFileName, Boolean createDirectoryIfNotExisting = False, Boolean overwriteFile = False) : object` |
| NameOfSmartContract | [BlockchainModule](./BlockchainModule.md) | `NameOfSmartContract(String contractAddressOrSymbol) : Object` |
| Navigate | [UiModule](./UiModule.md) | `Navigate(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object` |
| NavigateToUrl | [WebCrawlerModule](./WebCrawlerModule.md) | `NavigateToUrl(String url, String browserType = Chrome, Boolean headless = False, String profileName = , Boolean kioskMode = False, Dictionary<String, Object> argumentOptions = null, Nullable<Int32> timeoutInSeconds = null, Boolean hideTestingMode = False, Int32 pageIndex = -1, PLang.Models.GoalToCallInfo onRequest = null, PLang.Models.GoalToCallInfo onResponse = null, PLang.Models.GoalToCallInfo onWebsocketReceived = null, PLang.Models.GoalToCallInfo onWebsocketSent = null, PLang.Models.GoalToCallInfo onConsoleOutput = null, PLang.Models.GoalToCallInfo onWorker = null, PLang.Models.GoalToCallInfo onDialog = null, PLang.Models.GoalToCallInfo onLoad = null, PLang.Models.GoalToCallInfo onDOMLoad = null, PLang.Models.GoalToCallInfo onFileChooser = null, PLang.Models.GoalToCallInfo onIFrameLoad = null, PLang.Models.GoalToCallInfo onDownload = null) : object` |
| OnChangeVariablesListener | [VariableModule](./VariableModule.md) | `OnChangeVariablesListener(List<String> keys = null, PLang.Models.GoalToCallInfo goalName, Boolean notifyWhenCreated = True, Boolean waitForResponse = True, Int32 delayWhenNotWaitingInMilliseconds = 50) : object` |
| OnCreateVariablesListener | [VariableModule](./VariableModule.md) | `OnCreateVariablesListener(List<String> keys = null, PLang.Models.GoalToCallInfo goalName, Boolean waitForResponse = True, Int32 delayWhenNotWaitingInMilliseconds = 50) : object` |
| OnRemoveVariablesListener | [VariableModule](./VariableModule.md) | `OnRemoveVariablesListener(List<String> keys = null, PLang.Models.GoalToCallInfo goalName, Boolean waitForResponse = True, Int32 delayWhenNotWaitingInMilliseconds = 50) : object` |
| OpenFileInDefaultApp | [EnvironmentModule](./EnvironmentModule.md) | `OpenFileInDefaultApp(String filePath) : object` |
| Option | [HttpModule](./HttpModule.md) | `Option(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| Patch | [HttpModule](./HttpModule.md) | `Patch(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| Post | [HttpModule](./HttpModule.md) | `Post(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| PostMultipartFormData | [HttpModule](./HttpModule.md) | `PostMultipartFormData(String url, Object data, String httpMethod = POST, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, Int32 timeoutInSeconds = 30) : Object` |
| PrimeNumbers | [MathModule](./MathModule.md) | `PrimeNumbers(Int32 number) : List<Int32>` |
| Put | [HttpModule](./HttpModule.md) | `Put(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| QueryDynamicSql | [DbModule](./DbModule.md) | `QueryDynamicSql(List<String> dataSourceNames = null, String sql, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : PLang.Modules.DbModule.Table` |
| QuerySqlFile | [DbModule](./DbModule.md) | `QuerySqlFile(List<String> dataSourceNames = null, String fileName, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null, Nullable<Int32> rowsToReturn = null) : Object` |
| Read | [TerminalModule](./TerminalModule.md) | `Read(String variableName) : object` |
| ReadBinaryFileAndConvertToBase64 | [FileModule](./FileModule.md) | `ReadBinaryFileAndConvertToBase64(String path, String returnValueIfFileNotExisting = , Boolean throwErrorOnNotFound = False, Boolean includeDataUrl = False) : String` |
| ReadCsvFile | [FileModule](./FileModule.md) | `ReadCsvFile(String path, Boolean hasHeaderRecord = True, String delimiter = ,, String newLine = 
, String encoding = utf-8, Boolean ignoreBlankLines = True, Boolean allowComments = False, Char comment = #, PLang.Models.GoalToCallInfo goalToCallOnBadData = null) : Object` |
| ReadExcelFile | [FileModule](./FileModule.md) | `ReadExcelFile(String path, List<PLang.Modules.FileModule.Program+Sheet> sheetsToExtract = null) : Object` |
| ReadFileAsStream | [FileModule](./FileModule.md) | `ReadFileAsStream(String path, Boolean throwErrorOnNotFound = False) : IO.Stream` |
| ReadJson | [FileModule](./FileModule.md) | `ReadJson(String path, Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, Boolean allowReadingFromSystem = False) : Object` |
| ReadJsonLineFile | [FileModule](./FileModule.md) | `ReadJsonLineFile(String path, Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, String newLineSymbol = null, Boolean allowReadingFromSystem = False) : List<Object>` |
| ReadMultipleTextFiles | [FileModule](./FileModule.md) | `ReadMultipleTextFiles(String folderPath, String searchPattern = *, String[] excludePatterns = null, Boolean includeAllSubfolders = False) : List<FileInfo>` |
| ReadPdf | [FileModule](./FileModule.md) | `ReadPdf(String path, String format = md, String imagePath = null, String password = null) : PLang.Modules.FileModule.Program+Pdf` |
| ReadTextFile | [FileModule](./FileModule.md) | `ReadTextFile(String path, String returnValueIfFileNotExisting = , Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, String splitOn = null, Boolean allowReadingFromSystem = False) : Object` |
| ReadXml | [FileModule](./FileModule.md) | `ReadXml(String path) : Object` |
| Redirect | [WebserverModule](./WebserverModule.md) | `Redirect(String url, Boolean permanent = False, Boolean preserveMethod = False) : object` |
| ReloadPage | [WebCrawlerModule](./WebCrawlerModule.md) | `ReloadPage(Microsoft.Playwright.PageReloadOptions options = null) : object` |
| RemoveCache | [CachingModule](./CachingModule.md) | `RemoveCache(String key) : object` |
| RemoveDebugMode | [EnvironmentModule](./EnvironmentModule.md) | `RemoveDebugMode() : object` |
| RemoveElement | [UiModule](./UiModule.md) | `RemoveElement(List<PLang.Modules.UiModule.Program+UiRemove> domRemoves, String actor = user, String channel = default) : object` |
| RemoveHeaderFromRequest | [WebCrawlerModule](./WebCrawlerModule.md) | `RemoveHeaderFromRequest(String url, List<String> headersToRemove = null) : object` |
| RemoveSharedIdentity | [IdentityModule](./IdentityModule.md) | `RemoveSharedIdentity() : object` |
| RemoveVariables | [VariableModule](./VariableModule.md) | `RemoveVariables(String[] keys) : object` |
| RenderContent | [TemplateEngineModule](./TemplateEngineModule.md) | `RenderContent(String content, String fullPath = null, Dictionary<String, Object> variables = null) : String` |
| RenderFile | [TemplateEngineModule](./TemplateEngineModule.md) | `RenderFile(String path, Dictionary<String, Object> variables = null, Boolean writeToOutputStream = False) : String` |
| RenderImageToHtml | [UiModule](./UiModule.md) | `RenderImageToHtml(String path) : Object` |
| RenderTemplate | [UiModule](./UiModule.md) | `RenderTemplate(PLang.Modules.UiModule.Program+RenderTemplateOptions options) : Object` |
| Repeat | [LoopModule](./LoopModule.md) | `Repeat(Int32 repeatCounter, PLang.Models.GoalToCallInfo goalToCall, Int32 startIndex = 0) : object` |
| ReplaceState | [UiModule](./UiModule.md) | `ReplaceState(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object` |
| Request | [HttpModule](./HttpModule.md) | `Request(PLang.Modules.HttpModule.Program+HttpRequest request) : Object` |
| Request | [HttpModule](./HttpModule.md) | `Request(String url, String method, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object` |
| RequestAccessToPath | [FileModule](./FileModule.md) | `RequestAccessToPath(String path) : Boolean` |
| ResetSetup | [DbModule](./DbModule.md) | `ResetSetup(List<String> dataSourceNames = null) : object` |
| RestartWebserver | [WebserverModule](./WebserverModule.md) | `RestartWebserver(String webserverName = default) : PLang.Modules.WebserverModule.Program+WebserverProperties` |
| Retry | [ThrowErrorModule](./ThrowErrorModule.md) | `Retry(Int32 maxRetries = 1, String maxRetriesReachedMesage = null, String key = MaxRetries, Int32 statusCode = 400, String fixSuggestion = null, String helpfullLinks = null) : object` |
| Return | [VariableModule](./VariableModule.md) | `Return(Dictionary<String, Object> variables = null) : object` |
| Rollback | [DbModule](./DbModule.md) | `Rollback() : object` |
| Run | [PlangModule](./PlangModule.md) | `Run(String namespace, String class, String method, Dictionary<String, Object> Parameters = null) : Object` |
| RunAgent | [LlmModule](./LlmModule.md) | `RunAgent(String messages, List<PLang.Models.AgentTool> tools = null, String model = null, String reasoning = null, Int32 maxRounds = 30, PLang.Models.GoalToCallInfo onToolCall = null, PLang.Models.GoalToCallInfo onToolResult = null, PLang.Models.GoalToCallInfo onProgress = null, Int32 timeoutInSeconds = 600) : PLang.Modules.LlmModule.Program+AgentRun` |
| RunAi | [AiModule](./AiModule.md) | `RunAi(PLang.Modules.AiModule.AiInfo aiInfo) : Object` |
| RunApp | [AppModule](./AppModule.md) | `RunApp(PLang.Models.AppToCallInfo appToCall, Boolean waitForExecution = True, Int32 delayWhenNotWaitingInMilliseconds = 50, UInt32 waitForXMillisecondsBeforeRunningGoal = 0, Boolean keepMemoryStackOnAsync = False) : Object` |
| RunFileCode | [CodeModule](./CodeModule.md) | `RunFileCode(PLang.Modules.CodeModule.Builder+FileCodeImplementationResponse implementation) : Object` |
| RunFromStep | [PlangModule](./PlangModule.md) | `RunFromStep(String prFileName) : Object` |
| RunFunction | [PlangModule](./PlangModule.md) | `RunFunction(PLang.Runtime.ObjectValue genericFunction) : Object` |
| RunGoal | [CallGoalModule](./CallGoalModule.md) | `RunGoal(PLang.Models.GoalToCallInfo goalInfo, Boolean waitForExecution = True, Int32 delayWhenNotWaitingInMilliseconds = 50, UInt32 waitForXMillisecondsBeforeRunningGoal = 0, Boolean keepMemoryStackOnAsync = False, Boolean isolated = False, Boolean disableSystemGoals = False) : Object` |
| RunInlineCode | [CodeModule](./CodeModule.md) | `RunInlineCode(PLang.Services.CompilerService.CodeImplementationResponse implementation) : Object` |
| RunInlineCode | [ConditionalModule](./ConditionalModule.md) | `RunInlineCode(PLang.Services.CompilerService.ConditionImplementationResponse implementation) : Object` |
| RunLoop | [LoopModule](./LoopModule.md) | `RunLoop(String variableToLoopThrough, PLang.Models.GoalToCallInfo goalToCall, PLang.Modules.LoopModule.Program+MultiThreaded multiThreaded = null, PLang.Modules.LoopModule.Program+LinqOptions linqOptions = null) : object` |
| RunModule | [PlangModule](./PlangModule.md) | `RunModule(String moduleName, String method, Dictionary<String, Object> parameters = null, Boolean fromAppRoot = False) : Object` |
| RunPythonScript | [PythonModule](./PythonModule.md) | `RunPythonScript(String fileName = main.py, String[] parameterValues = null, String[] parameterNames = null, String[] variablesToExtractFromPythonScript = null, Boolean useNamedArguments = False, String pythonPath = null, String stdOutVariableName = null, String stdErrorVariableName = null) : object` |
| RunStep | [PlangModule](./PlangModule.md) | `RunStep(PLang.Building.Model.GoalStep step, Dictionary<String, Object> parameters = null) : Object` |
| RunTerminal | [TerminalModule](./TerminalModule.md) | `RunTerminal(String appExecutableName, List<String> parameters = null, String pathToWorkingDirInTerminal = null, String variableNameForDeltaOnStandardStream = null, String variableNameForDeltaOnErrorStream = null, Boolean hideTerminal = False) : Object` |
| RunWindowApp | [WindowAppModule](./WindowAppModule.md) | `RunWindowApp(PLang.Models.GoalToCallInfo goalName, Int32 width = 800, Int32 height = 450, String iconPath = null, String windowTitle = plang) : object` |
| SafeBatchTransferFromSmartContract | [BlockchainModule](./BlockchainModule.md) | `SafeBatchTransferFromSmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object` |
| SafeTransferFromErc1155SmartContract | [BlockchainModule](./BlockchainModule.md) | `SafeTransferFromErc1155SmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger[] ids, Numerics.BigInteger[] amounts, Boolean waitForReceipt = False) : Object` |
| SafeTransferFromErc721SmartContract | [BlockchainModule](./BlockchainModule.md) | `SafeTransferFromErc721SmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger id, Boolean waitForReceipt = False) : Object` |
| SaveGoal | [PlangModule](./PlangModule.md) | `SaveGoal(PLang.Building.Model.Goal goal) : Object` |
| SaveMethod | [PlangModule](./PlangModule.md) | `SaveMethod(Object methodPr) : Object` |
| SaveMultipleFiles | [FileModule](./FileModule.md) | `SaveMultipleFiles(List<PLang.Modules.FileModule.FileInfo> files, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8) : object` |
| Schedule | [ScheduleModule](./ScheduleModule.md) | `Schedule(String cronCommand, PLang.Models.GoalToCallInfo goalName, Nullable<DateTime> nextRun = null) : object` |
| ScrollToBottom | [WebCrawlerModule](./WebCrawlerModule.md) | `ScrollToBottom() : object` |
| ScrollToElement | [WebCrawlerModule](./WebCrawlerModule.md) | `ScrollToElement(PLang.Modules.WebCrawlerModule.PlangWebElement element) : object` |
| ScrollToElementByCssSelector | [WebCrawlerModule](./WebCrawlerModule.md) | `ScrollToElementByCssSelector(String cssSelector) : object` |
| Select | [DbModule](./DbModule.md) | `Select(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : PLang.Modules.DbModule.Table` |
| SelectByText | [WebCrawlerModule](./WebCrawlerModule.md) | `SelectByText(String text, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object` |
| SelectByValue | [WebCrawlerModule](./WebCrawlerModule.md) | `SelectByValue(String value, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object` |
| SelectOneRow | [DbModule](./DbModule.md) | `SelectOneRow(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : Object` |
| SelectOneRowWithMultipleDataSources | [DbModule](./DbModule.md) | `SelectOneRowWithMultipleDataSources(List<String> dataSourceNames = null, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : Object` |
| SelectWithMultipleDataSources | [DbModule](./DbModule.md) | `SelectWithMultipleDataSources(List<String> dataSourceNames = null, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : PLang.Modules.DbModule.Table` |
| Send | [WebSocketModule](./WebSocketModule.md) | `Send(Object message, String name = null) : object` |
| SendBinaryOfFile | [HttpModule](./HttpModule.md) | `SendBinaryOfFile(String url, String filePath, String httpMethod = POST, Dictionary<String, Object> requestHeaders = null, Dictionary<String, Object> contentHeaders = null, String encoding = utf-8, Int32 timeoutInSeconds = 30) : Object` |
| SendEmail | [MessageModule](./MessageModule.md) | `SendEmail(PLang.Modules.MessageModule.Program+EmailMessage emailMessage) : Object` |
| SendFileToUser | [WebserverModule](./WebserverModule.md) | `SendFileToUser(String path, String fileName = null, String contentType = null, String actor = user, String channel = default) : object` |
| SendKey | [WebCrawlerModule](./WebCrawlerModule.md) | `SendKey(String value, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null, Boolean humanStyle = False) : object` |
| SendPrivateMessage | [MessageModule](./MessageModule.md) | `SendPrivateMessage(String content, String receiverPublicKey) : object` |
| SendPrivateMessageToMyself | [MessageModule](./MessageModule.md) | `SendPrivateMessageToMyself(String content) : object` |
| SendToWebSocket | [WebserverModule](./WebserverModule.md) | `SendToWebSocket(Object data, Dictionary<String, Object> headers = null, String webSocketName = default) : object` |
| SendToWebSocket | [WebserverModule](./WebserverModule.md) | `SendToWebSocket(PLang.Models.GoalToCallInfo goalToCall, Dictionary<String, Object> parameters = null, String webSocketName = default) : object` |
| SendTransaction | [BlockchainModule](./BlockchainModule.md) | `SendTransaction(String contractAddress, String abi, Object[] args) : String` |
| SendTransactionAndWaitForReceipt | [BlockchainModule](./BlockchainModule.md) | `SendTransactionAndWaitForReceipt(String contractAddress, String abi, Object[] args) : Nethereum.RPC.Eth.DTOs.TransactionReceipt` |
| Serialize | [SerializerModule](./SerializerModule.md) | `Serialize(Object data, String serializer = json, IO.Stream stream = null) : Byte[]` |
| SetApprovalForAllOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `SetApprovalForAllOnSmartContract(String contractAddressOrSymbol, String operatorAddress, Boolean approved, Boolean waitForReceipt = False) : Object` |
| SetBoolVariable | [VariableModule](./VariableModule.md) | `SetBoolVariable(String key, Nullable<Boolean> value = null, Nullable<Boolean> defaultValue = null) : object` |
| SetBoolVariableWithCondition | [VariableModule](./VariableModule.md) | `SetBoolVariableWithCondition(String key, PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition simpleCondition, Nullable<Boolean> defaultValue = null) : object` |
| SetCertificate | [WebserverModule](./WebserverModule.md) | `SetCertificate(String permFilePath, String privateKeyFile = null) : object` |
| SetCultureLanguageCode | [EnvironmentModule](./EnvironmentModule.md) | `SetCultureLanguageCode(String code = en-US) : object` |
| SetCultureUILanguageCode | [EnvironmentModule](./EnvironmentModule.md) | `SetCultureUILanguageCode(String code = en-US) : object` |
| SetCurrentAccount | [MessageModule](./MessageModule.md) | `SetCurrentAccount(String publicKeyOrName) : object` |
| SetCurrentAddress | [BlockchainModule](./BlockchainModule.md) | `SetCurrentAddress(String address) : object` |
| SetCurrentBearerToken | [CryptographicModule](./CryptographicModule.md) | `SetCurrentBearerToken(String name) : object` |
| SetCurrentIdentity | [IdentityModule](./IdentityModule.md) | `SetCurrentIdentity(String nameOrIdentity) : PLang.Interfaces.Identity` |
| SetCurrentRpcServer | [BlockchainModule](./BlockchainModule.md) | `SetCurrentRpcServer(String nameOrUrl) : object` |
| SetCurrentWallet | [BlockchainModule](./BlockchainModule.md) | `SetCurrentWallet(String walletName) : object` |
| SetDataSourceNames | [DbModule](./DbModule.md) | `SetDataSourceNames(List<String> dataSourceNames = null) : PLang.Modules.DbModule.ModuleSettings+DataSource` |
| SetDateTimeVariable | [VariableModule](./VariableModule.md) | `SetDateTimeVariable(String key, Object value = null) : object` |
| SetDebugMode | [EnvironmentModule](./EnvironmentModule.md) | `SetDebugMode() : object` |
| SetDefaultNumberVariable | [VariableModule](./VariableModule.md) | `SetDefaultNumberVariable(String key, Nullable<Int64> value = null, Nullable<Int64> defaultValue = null, Nullable<Int64> maxValue = null, Nullable<Int64> minValue = null) : object` |
| SetDefaultSettingValue | [VariableModule](./VariableModule.md) | `SetDefaultSettingValue(String key, Object value) : object` |
| SetDefaultValueOnVariables | [VariableModule](./VariableModule.md) | `SetDefaultValueOnVariables(Dictionary<String, Object> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object` |
| SetDoubleVariable | [VariableModule](./VariableModule.md) | `SetDoubleVariable(String key, Double value, Nullable<Double> maxValue = null, Nullable<Double> minValue = null) : object` |
| SetDoubleVariables | [VariableModule](./VariableModule.md) | `SetDoubleVariables(Dictionary<String, Double> values = null) : object` |
| SetElement | [UiModule](./UiModule.md) | `SetElement(List<PLang.Modules.UiModule.Program+UiInstruction> uiInstructions, String actor = user, String channel = default) : object` |
| SetEnvironment | [EnvironmentModule](./EnvironmentModule.md) | `SetEnvironment(String name) : object` |
| SetFloatVariable | [VariableModule](./VariableModule.md) | `SetFloatVariable(String key, Nullable<Single> value = null, Nullable<Single> defaultValue = null) : object` |
| SetFloatVariable | [VariableModule](./VariableModule.md) | `SetFloatVariable(Dictionary<String, Single> values = null) : object` |
| SetFocus | [WebCrawlerModule](./WebCrawlerModule.md) | `SetFocus(String cssSelector = null, Nullable<Int32> timoutInSeconds = null) : object` |
| SetForAbsoluteExpiration | [CachingModule](./CachingModule.md) | `SetForAbsoluteExpiration(String key, Object value, Int32 timeInSeconds = 600) : object` |
| SetForSlidingExpiration | [CachingModule](./CachingModule.md) | `SetForSlidingExpiration(String key, Object value, Int32 timeInSeconds = 600) : object` |
| SetFrameworks | [UiModule](./UiModule.md) | `SetFrameworks(PLang.Modules.UiModule.Program+UiFramework framework) : object` |
| SetHtmlSanitizerOptions | [OptionsModule](./OptionsModule.md) | `SetHtmlSanitizerOptions(Ganss.Xss.HtmlSanitizerOptions options, Boolean keepDefaults = True) : object` |
| SetJsonObjectVariable | [VariableModule](./VariableModule.md) | `SetJsonObjectVariable(String key, Object value = null, Boolean doNotLoadVariablesInValue = False, Object defaultValue = null) : object` |
| SetLayout | [UiModule](./UiModule.md) | `SetLayout(PLang.Modules.UiModule.Program+LayoutOptions options) : List<LayoutOptions>` |
| SetNumberVariable | [VariableModule](./VariableModule.md) | `SetNumberVariable(String key, Nullable<Int64> value = null, Nullable<Int64> defaultValue = null, Nullable<Int64> maxValue = null, Nullable<Int64> minValue = null) : object` |
| SetOutputStream | [OutputModule](./OutputModule.md) | `SetOutputStream(String channel, PLang.Models.GoalToCallInfo goalToCall, Dictionary<String, Object> parameters = null) : object` |
| SetSelfSignedCertificate | [WebserverModule](./WebserverModule.md) | `SetSelfSignedCertificate() : object` |
| SetSettingsDbPath | [EnvironmentModule](./EnvironmentModule.md) | `SetSettingsDbPath(String path) : object` |
| SetSettingValue | [VariableModule](./VariableModule.md) | `SetSettingValue(String key, Object value) : object` |
| SetStringVariable | [VariableModule](./VariableModule.md) | `SetStringVariable(String key, String value = null, Boolean urlDecode = False, Boolean htmlDecode = False, Boolean doNotLoadVariablesInValue = False, String defaultValue = null) : object` |
| SetTargetArea | [UiModule](./UiModule.md) | `SetTargetArea(String target) : object` |
| SetTextOnElement | [WebCrawlerModule](./WebCrawlerModule.md) | `SetTextOnElement(String text, String cssSelector = null, Nullable<Int32> timeoutInSeconds = null, Boolean clearElementFirst = False) : object` |
| SetValueAndStore | [VariableModule](./VariableModule.md) | `SetValueAndStore(Dictionary<String, Object> variables = null, String dataSourceName = null) : object` |
| SetValueOnVariablesOrDefaultIfValueIsEmpty | [VariableModule](./VariableModule.md) | `SetValueOnVariablesOrDefaultIfValueIsEmpty(List<PLang.Modules.VariableModule.Program+VariableIfEmpty> variables, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object` |
| SetValuesOnVariables | [VariableModule](./VariableModule.md) | `SetValuesOnVariables(Dictionary<String, Object> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object` |
| SetVariable | [VariableModule](./VariableModule.md) | `SetVariable(String key, Object value = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null, Object defaultValue = null, String FullTypeName = null) : object` |
| SetVariables | [VariableModule](./VariableModule.md) | `SetVariables(Dictionary<String, Tuple<Object, Object>> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object` |
| SetVariableWithCalculation | [VariableModule](./VariableModule.md) | `SetVariableWithCalculation(String key, String expression, Int32 decimalRound = 2, Nullable<MidpointRounding> midpointRounding = null) : object` |
| SetVariableWithCondition | [ConditionalModule](./ConditionalModule.md) | `SetVariableWithCondition(String variableName, Boolean boolValue, Object valueIfTrue, Object valueIfFalse) : object` |
| SetVariableWithCondition | [VariableModule](./VariableModule.md) | `SetVariableWithCondition(String variableName, Object value, Object valueIfTrue, Object valueIfFalse) : object` |
| ShowElement | [UiModule](./UiModule.md) | `ShowElement(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object` |
| ShowErrorDetails | [EnvironmentModule](./EnvironmentModule.md) | `ShowErrorDetails() : object` |
| ShowNotification | [UiModule](./UiModule.md) | `ShowNotification(PLang.Services.OutputStream.Messages.TextMessage textMessage) : object` |
| ShutdownWebserver | [WebserverModule](./WebserverModule.md) | `ShutdownWebserver(String webserverName) : PLang.Modules.WebserverModule.Program+WebserverProperties` |
| Sign | [IdentityModule](./IdentityModule.md) | `Sign(Object body, List<String> contracts = null, Nullable<Int32> expiresInSeconds = null, Dictionary<String, Object> headers = null, Boolean skipNonce = False) : PLang.Models.SignedMessage` |
| SignIntoProperty | [IdentityModule](./IdentityModule.md) | `SignIntoProperty(Object body, String property) : Object` |
| SignTransfer | [BlockchainModule](./BlockchainModule.md) | `SignTransfer(String recipient, String smartContractSymbolOrAddress, Int64 value) : String` |
| SimpleCondition | [ConditionalModule](./ConditionalModule.md) | `SimpleCondition(PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition condition, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| Sleep | [ScheduleModule](./ScheduleModule.md) | `Sleep(Int32 sleepTimeInMilliseconds) : object` |
| SolveExpression | [MathModule](./MathModule.md) | `SolveExpression(String expression, Int32 decimalRound = 2, Nullable<MidpointRounding> midpointRounding = null) : Object` |
| StartBrowser | [WebCrawlerModule](./WebCrawlerModule.md) | `StartBrowser(String browserType = Chrome, Boolean headless = False, String profileName = , Boolean kioskMode = False, Dictionary<String, Object> argumentOptions = null, Nullable<Int32> timoutInSeconds = null, Boolean hideTestingMode = False, PLang.Models.GoalToCallInfo onRequest = null, PLang.Models.GoalToCallInfo onResponse = null) : PLang.Modules.WebCrawlerModule.Models.BrowserInstance` |
| StartCSharpDebugger | [PlangModule](./PlangModule.md) | `StartCSharpDebugger() : object` |
| StartScheduler | [ScheduleModule](./ScheduleModule.md) | `StartScheduler() : object` |
| StartsWith | [ConditionalModule](./ConditionalModule.md) | `StartsWith(Object item, String startsWith, Boolean isNot = False, Boolean ignoreCase = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object` |
| StartWebserver | [WebserverModule](./WebserverModule.md) | `StartWebserver(PLang.Modules.WebserverModule.Program+WebserverProperties webserverProperties) : PLang.Modules.WebserverModule.Program+WebserverProperties` |
| StartWebSocketConnection | [WebserverModule](./WebserverModule.md) | `StartWebSocketConnection(String url, PLang.Models.GoalToCallInfo goalToCall, String webSocketName = default, String contentRecievedVariableName = %content%) : PLang.Modules.WebserverModule.Program+WebSocketInfo` |
| StopListening | [BlockchainModule](./BlockchainModule.md) | `StopListening(String subscriptionId) : object` |
| StopListeningToFileChange | [FileModule](./FileModule.md) | `StopListeningToFileChange(String[] fileSearchPatterns, PLang.Models.GoalToCallInfo goalToCall = null) : object` |
| Store | [VariableModule](./VariableModule.md) | `Store(List<String> variables = null, String dataSourceName = null) : object` |
| StreamFile | [WebserverModule](./WebserverModule.md) | `StreamFile(PLang.Services.OutputStream.Messages.StreamMessage streamMessage, Int64 startByte = 0, Nullable<Int64> endByte = null) : object` |
| Submit | [WebCrawlerModule](./WebCrawlerModule.md) | `Submit(String cssSelector = null, Nullable<Int32> timeoutInSeconds = null) : object` |
| SupportsInterfaceOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `SupportsInterfaceOnSmartContract(String contractAddressOrSymbol, String interfaceId) : Object` |
| SwitchTab | [WebCrawlerModule](./WebCrawlerModule.md) | `SwitchTab(Int32 tabIndex) : object` |
| SymbolOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `SymbolOnSmartContract(String contractAddressOrSymbol) : Object` |
| TakeScreenshotOfWebsite | [WebCrawlerModule](./WebCrawlerModule.md) | `TakeScreenshotOfWebsite(String saveToPath, Boolean overwrite = False, String cssSelector = null) : object` |
| Throw | [ThrowErrorModule](./ThrowErrorModule.md) | `Throw(Object message, String key = UserDefinedError, Int32 statusCode = 400, String fixSuggestion = null, String helpfullLinks = null) : object` |
| ThrowError | [ThrowErrorModule](./ThrowErrorModule.md) | `ThrowError(PLang.Services.OutputStream.Messages.ErrorMessage errorMessage) : object` |
| TotalSupplyOnSmartContract | [BlockchainModule](./BlockchainModule.md) | `TotalSupplyOnSmartContract(String contractAddressOrSymbol) : Object` |
| Transfer | [BlockchainModule](./BlockchainModule.md) | `Transfer(String to, Decimal etherAmount, Nullable<Decimal> gasPriceWei = null, Nullable<Numerics.BigInteger> gas = null, Nullable<Numerics.BigInteger> nonce = null) : String` |
| TransferFromSmartContract | [BlockchainModule](./BlockchainModule.md) | `TransferFromSmartContract(String contractAddressOrSymbol, String from, String to, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object` |
| TransferSmartContract | [BlockchainModule](./BlockchainModule.md) | `TransferSmartContract(String contractAddressOrSymbol, String to, Numerics.BigInteger value, Boolean waitForReceipt = False) : Object` |
| TransferWaitForReceipt | [BlockchainModule](./BlockchainModule.md) | `TransferWaitForReceipt(String to, Decimal etherAmount, Nullable<Decimal> gasPriceWei = null, Nullable<Numerics.BigInteger> gas = null, Nullable<Numerics.BigInteger> nonce = null) : Nethereum.RPC.Eth.DTOs.TransactionReceipt` |
| TrimForLlm | [VariableModule](./VariableModule.md) | `TrimForLlm(Object obj, Int32 maxItemCount = 30, Nullable<Int32> maxItemLength = null, String groupOn = null, Int32 samplesPerGroup = 5, Int32 listLimit = 50, Int32 totalCharsLimit = 2000, Boolean formatJson = False) : Object` |
| Update | [DbModule](./DbModule.md) | `Update(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64` |
| UpdateWithJsonColumns | [DbModule](./DbModule.md) | `UpdateWithJsonColumns(String dataSourceName, String table, String jsonOfColumns, List<String> allowColumns = null, String whereStatment = null, List<PLang.Modules.DbModule.Program+ParameterInfo> whereParameter = null) : Int64` |
| UseSharedIdentity | [IdentityModule](./IdentityModule.md) | `UseSharedIdentity() : object` |
| UseSharedIdentity | [LlmModule](./LlmModule.md) | `UseSharedIdentity(Boolean useSharedIdentity = True) : object` |
| ValidateBearerToken | [CryptographicModule](./CryptographicModule.md) | `ValidateBearerToken(String token, String issuer = PLangRuntime, String audience = user) : Boolean` |
| ValidateFileExtension | [ValidateModule](./ValidateModule.md) | `ValidateFileExtension(List<String> allowedExtensions = null, String fileName) : object` |
| ValidateGoal | [PlangModule](./PlangModule.md) | `ValidateGoal(PLang.Building.Model.Goal goal) : Object` |
| ValidateItemIsInList | [ValidateModule](./ValidateModule.md) | `ValidateItemIsInList(Object[] itemsToCheckInList, Collections.IList list, String errorMessage = item is not in list, Boolean caseSensitive = False) : List<Object>` |
| ValidateMethod | [PlangModule](./PlangModule.md) | `ValidateMethod(PLang.Building.Model.GoalStep step, Object function) : Object` |
| ValidateSignedContract | [ValidateModule](./ValidateModule.md) | `ValidateSignedContract(Object contract, List<String> signatureProperties = null, List<String> propertiesToMatch = null) : object` |
| ValidateToken | [CryptographicModule](./CryptographicModule.md) | `ValidateToken(String token, String password = null, String secretKeyName = null) : object` |
| ValidateType | [ValidateModule](./ValidateModule.md) | `ValidateType(PLang.Modules.ValidateModule.Program+TypeValidation validation) : object` |
| VerifyHashedValues | [CryptographicModule](./CryptographicModule.md) | `VerifyHashedValues(String text, String hash, String hashAlgorithm = keccak256, Boolean useSalt = True, String salt = null) : Boolean` |
| VerifyHashOfFile | [CryptographicModule](./CryptographicModule.md) | `VerifyHashOfFile(String filePath, String expectedHash, String hashAlgorithm = sha256, String encoding = base64) : Boolean` |
| VerifySignature | [IdentityModule](./IdentityModule.md) | `VerifySignature(Object signatureFromUser = null, Dictionary<String, Object> headers = null, Object body = null, List<String> contracts = null) : PLang.Models.SignedMessage` |
| VerifySignatureOnProperties | [IdentityModule](./IdentityModule.md) | `VerifySignatureOnProperties(Object signatureFromUser, List<String> properties = null) : PLang.Models.SignedMessage` |
| Wait | [WebCrawlerModule](./WebCrawlerModule.md) | `Wait(Int32 milliseconds = 1000) : object` |
| WaitForElementToAppear | [WebCrawlerModule](./WebCrawlerModule.md) | `WaitForElementToAppear(String cssSelector, Int32 timeoutInSeconds = 30, Boolean waitForElementToChange = False) : object` |
| WaitForFile | [FileModule](./FileModule.md) | `WaitForFile(String filePath, Int32 timeoutInMilliseconds = 30000, Boolean waitForAccess = False) : object` |
| WaitForUrl | [WebCrawlerModule](./WebCrawlerModule.md) | `WaitForUrl(String expectedUrl, Int32 timeoutInSeconds) : object` |
| WaitIncreasingly | [ScheduleModule](./ScheduleModule.md) | `WaitIncreasingly(String key, List<Int32> millisecondsDelay = null, Int32 timeoutInSeconds = 300) : object` |
| WaitOnVariable | [ScheduleModule](./ScheduleModule.md) | `WaitOnVariable(String variableName, PLang.Models.GoalToCallInfo goalToCall, Int64 timeInMilliseconds = 1000) : object` |
| Write | [OutputModule](./OutputModule.md) | `Write(PLang.Services.OutputStream.Messages.TextMessage textMessage) : object` |
| WriteBase64ToFile | [FileModule](./FileModule.md) | `WriteBase64ToFile(String path, String base64, Boolean overwrite = False) : object` |
| WriteBytesToFile | [FileModule](./FileModule.md) | `WriteBytesToFile(String path, Byte[] content, Boolean overwrite = False) : object` |
| WriteCookie | [WebserverModule](./WebserverModule.md) | `WriteCookie(String name, String value, Int32 expiresInSeconds = 604800) : object` |
| WriteCsvFile | [FileModule](./FileModule.md) | `WriteCsvFile(String path, Object variableToWriteToCsv, Boolean appendToFile = False, PLang.Modules.FileModule.CsvHelper+CsvOptions csvOptions = null, Boolean createDirectoryAutomatically = True) : object` |
| WriteExcelFile | [FileModule](./FileModule.md) | `WriteExcelFile(String path, Object variableToWriteToExcel, String sheetName = Sheet1, Boolean printHeader = True, Boolean overwrite = False) : object` |
| WriteJson | [OutputModule](./OutputModule.md) | `WriteJson(PLang.Services.OutputStream.Messages.TextMessage textMessage, PLang.Modules.OutputModule.Program+JsonOptions jsonOptions = null) : object` |
| WriteToFile | [FileModule](./FileModule.md) | `WriteToFile(String path, Object content, Boolean overwrite = False, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8) : object` |
| WriteToResponseHeader | [WebserverModule](./WebserverModule.md) | `WriteToResponseHeader(Dictionary<String, Object> headers = null) : object` |
| WriteVariablesToCookie | [WebserverModule](./WebserverModule.md) | `WriteVariablesToCookie(String name, List<PLang.Runtime.ObjectValue> values, Int32 expiresInSeconds = 604800) : object` |
| WriteWithStreamInfo | [OutputModule](./OutputModule.md) | `WriteWithStreamInfo(Object content, PLang.Modules.OutputModule.Program+OutputStreamInfo outputStreamInfo) : object` |

## Gaps

7 modules and 264 of 462 methods carry no
`[Description]`, so the builder has nothing but the name to match a step against. Those are the
ones the llm guesses at, and the ones worth writing first.

Modules: AiModule, LoggerModule, SerializerModule, UdpModule, WebSocketModule, WindowAppModule, XmlModule

<details><summary>Methods with no description</summary>

- BlockchainModule.AllowanceFromSmartContract
- BlockchainModule.ApproveSmartContract
- BlockchainModule.BalanceOfBatchOnSmartContract
- BlockchainModule.BalanceOfOnSmartContract
- BlockchainModule.BurnSmartContract
- BlockchainModule.CallFunction
- BlockchainModule.CecimalsOnSmartContract
- BlockchainModule.GetApprovedOnSmartContract
- BlockchainModule.GetCurrentAddress
- BlockchainModule.GetCurrentRpcServer
- BlockchainModule.GetDecimal
- BlockchainModule.GetMyBalanceOnSmartContract
- BlockchainModule.GetNativeBalanceOfAddressInWei
- BlockchainModule.GetOrCreateWallet
- BlockchainModule.GetPrivateKey
- BlockchainModule.GetRpcServers
- BlockchainModule.GetUriOnSmartContract
- BlockchainModule.GetWallets
- BlockchainModule.IsApprovedForAllOnSmartContract
- BlockchainModule.ListenToApprovalEventOnSmartContract
- BlockchainModule.ListenToApprovalForAllEventOnSmartContract
- BlockchainModule.ListenToBlock
- BlockchainModule.ListenToEventOnSmartContract
- BlockchainModule.ListenToTransferBatchEventOnSmartContract
- BlockchainModule.ListenToTransferEventOnSmartContract
- BlockchainModule.ListenToTransferSingleEventOnSmartContract
- BlockchainModule.ListenToUriEventOnSmartContract
- BlockchainModule.MintSmartContract
- BlockchainModule.NameOfSmartContract
- BlockchainModule.SafeBatchTransferFromSmartContract
- BlockchainModule.SafeTransferFromErc1155SmartContract
- BlockchainModule.SafeTransferFromErc721SmartContract
- BlockchainModule.SetApprovalForAllOnSmartContract
- BlockchainModule.SetCurrentAddress
- BlockchainModule.SetCurrentRpcServer
- BlockchainModule.SetCurrentWallet
- BlockchainModule.SignTransfer
- BlockchainModule.StopListening
- BlockchainModule.SupportsInterfaceOnSmartContract
- BlockchainModule.SymbolOnSmartContract
- BlockchainModule.TotalSupplyOnSmartContract
- BlockchainModule.Transfer
- BlockchainModule.TransferFromSmartContract
- BlockchainModule.TransferSmartContract
- BlockchainModule.TransferWaitForReceipt
- CachingModule.Get
- CachingModule.RemoveCache
- CachingModule.SetForAbsoluteExpiration
- CachingModule.SetForSlidingExpiration
- CodeModule.RunFileCode
- CodeModule.RunInlineCode
- CompressionModule.CompressDirectory
- CompressionModule.CompressFiles
- CompressionModule.DecompressFile
- ConditionalModule.DirectoryExists
- ConditionalModule.FileExists
- ConditionalModule.HasAccessToPath
- ConditionalModule.IsBase64
- ConditionalModule.IsEmpty
- ConditionalModule.IsFalse
- ConditionalModule.IsMod
- ConditionalModule.IsNotEmpty
- ConditionalModule.IsNotEqual
- ConditionalModule.IsOsPath
- ConditionalModule.IsSystemPath
- ConditionalModule.IsTrue
- ConditionalModule.SetVariableWithCondition
- ConvertModule.ConvertMdToHtml
- ConvertModule.ConvertToKeyValueList
- CryptographicModule.AddPrivateKey
- CryptographicModule.ConvertFromBase64
- CryptographicModule.ConvertToBase64
- CryptographicModule.CreateSalt
- CryptographicModule.Decrypt
- CryptographicModule.Encrypt
- CryptographicModule.GenerateBearerToken
- CryptographicModule.GetBearerSecret
- CryptographicModule.GetHashOfFile
- CryptographicModule.GetPrivateKey
- CryptographicModule.GetPrivateKeyHash
- CryptographicModule.SetCurrentBearerToken
- CryptographicModule.ValidateBearerToken
- DbModule.BeginTransaction
- DbModule.Delete
- DbModule.EndTransaction
- DbModule.GetAdditionalAssistantErrorInfo
- DbModule.GetAdditionalSystemErrorInfo
- DbModule.LoadExtension
- DbModule.ResetSetup
- DbModule.Rollback
- DbModule.Update
- DesktopModule.GetOpenWindows
- EnvironmentModule.CanSeeErrorDetails
- EnvironmentModule.EndApp
- EnvironmentModule.GetAppName
- EnvironmentModule.GetCurrentCulture
- EnvironmentModule.GetEnvironmentVariable
- EnvironmentModule.GetMachineName
- EnvironmentModule.GetOSDescription
- EnvironmentModule.GetProcessId
- EnvironmentModule.GetSettings
- EnvironmentModule.GetUserName
- EnvironmentModule.HideErrorDetails
- EnvironmentModule.InstallNpm
- EnvironmentModule.IsInCSharpDebugMode
- EnvironmentModule.IsInDebugMode
- EnvironmentModule.KeepAlive
- EnvironmentModule.OpenFileInDefaultApp
- EnvironmentModule.RemoveDebugMode
- EnvironmentModule.SetDebugMode
- EnvironmentModule.SetEnvironment
- EnvironmentModule.SetSettingsDbPath
- EnvironmentModule.ShowErrorDetails
- EventModule.BindEvent
- FileModule.AddTypeMapping
- FileModule.AppendToFile
- FileModule.CopyFile
- FileModule.CopyFiles
- FileModule.CreateDirectory
- FileModule.DeleteDirectory
- FileModule.DeleteFile
- FileModule.FileExists
- FileModule.GetDirectoryPathsInDirectory
- FileModule.GetFileInfo
- FileModule.GetFileType
- FileModule.GiveAccess
- FileModule.MoveFile
- FileModule.ReadCsvFile
- FileModule.ReadFileAsStream
- FileModule.ReadJson
- FileModule.ReadJsonLineFile
- FileModule.ReadMultipleTextFiles
- FileModule.ReadXml
- FileModule.SaveMultipleFiles
- FileModule.StopListeningToFileChange
- FileModule.WaitForFile
- FileModule.WriteBase64ToFile
- FileModule.WriteBytesToFile
- FileModule.WriteCsvFile
- FileModule.WriteExcelFile
- FileModule.WriteToFile
- FilterModule.ExtractFromXPath
- HttpModule.Delete
- HttpModule.DownloadFile
- HttpModule.Get
- HttpModule.Head
- HttpModule.Option
- HttpModule.Patch
- HttpModule.Post
- HttpModule.Put
- HttpModule.Request
- HttpModule.Request
- IdentityModule.GetPrivateKey
- IdentityModule.RemoveSharedIdentity
- ListDictionaryModule.ClearDictionary
- ListDictionaryModule.ClearList
- ListDictionaryModule.DeleteFromList
- ListDictionaryModule.DeleteKeyFromDictionary
- ListDictionaryModule.GetItem
- LlmModule.AppendToAssistant
- LlmModule.AppendToSystem
- LlmModule.AppendToUser
- LlmModule.GetLlmIdentity
- LlmModule.UseSharedIdentity
- MessageModule.GetPrivateKey
- MessageModule.GetPublicKey
- MessageModule.GetRelays
- MessageModule.SendEmail
- MessageModule.SendPrivateMessage
- MessageModule.SendPrivateMessageToMyself
- MessageModule.SetCurrentAccount
- MockModule.MockMethod
- OutputModule.SetOutputStream
- OutputModule.WriteWithStreamInfo
- PlangModule.GetMehodInfo
- PlangModule.GetMethodMappingScheme
- PlangModule.GetMethods
- PlangModule.GetModules
- PlangModule.GetModules2
- PlangModule.GetStep
- PlangModule.GetStepProperties
- PlangModule.GetSteps
- PlangModule.GetVariables
- PlangModule.Run
- PlangModule.RunFunction
- PlangModule.SaveGoal
- PlangModule.SaveMethod
- PlangModule.StartCSharpDebugger
- PlangModule.ValidateGoal
- PlangModule.ValidateMethod
- ScheduleModule.StartScheduler
- ScheduleModule.WaitOnVariable
- SerializerModule.AddSerializer
- SerializerModule.ConvertToType
- SerializerModule.Deserialize
- SerializerModule.Deserialize
- TemplateEngineModule.RenderContent
- TerminalModule.Read
- UdpModule.Get
- UiModule.CloseWindow
- UiModule.ExecuteJavascript
- UiModule.Flush
- UiModule.RenderImageToHtml
- UiModule.SetElement
- UiModule.SetFrameworks
- ValidateModule.ValidateFileExtension
- ValidateModule.ValidateItemIsInList
- VariableModule.ConvertToBase64
- VariableModule.GetEnvironmentVariable
- VariableModule.GetSettings
- VariableModule.GetVariable
- VariableModule.Load
- VariableModule.LoadVariables
- VariableModule.OnChangeVariablesListener
- VariableModule.OnCreateVariablesListener
- VariableModule.OnRemoveVariablesListener
- VariableModule.RemoveVariables
- VariableModule.SetVariableWithCondition
- VariableModule.TrimForLlm
- WebCrawlerModule.AcceptAlert
- WebCrawlerModule.AddDefaultRequestHeader
- WebCrawlerModule.AssertPageContains
- WebCrawlerModule.Click
- WebCrawlerModule.ClickOnElement
- WebCrawlerModule.CloseBrowser
- WebCrawlerModule.ExtractClassesToList
- WebCrawlerModule.FindElementAndExtractAttribute
- WebCrawlerModule.GetBrowserInstance
- WebCrawlerModule.GetElements
- WebCrawlerModule.GetElementsInsideElement
- WebCrawlerModule.GetPage
- WebCrawlerModule.GetUriFromPage
- WebCrawlerModule.ReloadPage
- WebCrawlerModule.ScrollToBottom
- WebCrawlerModule.ScrollToElement
- WebCrawlerModule.ScrollToElementByCssSelector
- WebCrawlerModule.SetFocus
- WebCrawlerModule.Submit
- WebCrawlerModule.SwitchTab
- WebCrawlerModule.TakeScreenshotOfWebsite
- WebCrawlerModule.Wait
- WebCrawlerModule.WaitForElementToAppear
- WebCrawlerModule.WaitForUrl
- WebserverModule.DeleteCookie
- WebserverModule.GetCookie
- WebserverModule.GetCookieRaw
- WebserverModule.GetNumberOfLiveConnections
- WebserverModule.GetRequestHeader
- WebserverModule.Redirect
- WebserverModule.RestartWebserver
- WebserverModule.SendFileToUser
- WebserverModule.SendToWebSocket
- WebserverModule.SendToWebSocket
- WebserverModule.SetCertificate
- WebserverModule.SetSelfSignedCertificate
- WebserverModule.ShutdownWebserver
- WebserverModule.StartWebserver
- WebserverModule.StartWebSocketConnection
- WebserverModule.StreamFile
- WebserverModule.WriteCookie
- WebserverModule.WriteToResponseHeader
- WebserverModule.WriteVariablesToCookie
- WebSocketModule.Connect
- WebSocketModule.Send

</details>
