using Microsoft.Extensions.Logging;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using System.Collections.Generic;
using System.IO.Abstractions;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.UiModule.Program;

namespace PLang.Modules.UiModule

{
	public class Builder : BaseBuilder
	{
		private readonly ILogger logger;
		private readonly ITypeHelper typeHelper;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly IEngine engine;
		private readonly VariableHelper variableHelper;

		public Builder(ILogger logger, ITypeHelper typeHelper, ILlmServiceFactory llmServiceFactory, IPLangFileSystem fileSystem, IEngine engine, VariableHelper variableHelper) : base()
		{
			this.logger = logger;
			this.typeHelper = typeHelper;
			this.llmServiceFactory = llmServiceFactory;
			this.fileSystem = fileSystem;
			this.engine = engine;
			this.variableHelper = variableHelper;
		}


		public override async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			/*
			SetSystem(@"Your job is: 
1. Parse user intent
2. Map the intent to one of C# function available provided to you
3. Return a valid JSON

Variable is defined with starting and ending %, e.g. %filePath%. 
%Variable% MUST be wrapped in quotes("") in json response, e.g. { ""name"":""%name%"" }
Variables should not be changed, they can include dot(.) and parentheses()
Keep \n, \r, \t	 that are submitted to you for string variables

If there is some api key, settings, config replace it with %Settings.Get(""settingName"", ""defaultValue"", ""Explain"")% 
- settingName would be the api key, config key, 
- defaultValue for settings is the usual value given, make it """" if no value can be default
- Explain is an explanation about the setting that novice user can understand.

The user is build or manipulating user interface written in html. His intent will reflect that
Response with only the function name you would choose");

			var classDescriptionHelper = new ClassDescriptionHelper();

			var result = classDescriptionHelper.GetClassDescription(typeof(Program));
			if (result.Error != null) return (null,  result.Error);

			SetAssistant($"### function available ###\n{JsonConvert.SerializeObject(result.ClassDescription)}\n### function available ###");

			var buildFunctionName = await base.Build<GenericFunction>(step);

			var functionName = buildFunctionName.Instruction.Function;


			if (buildFunctionName.BuilderError != null) return (null, buildFunctionName.BuilderError);

			if (functionName.Name != "RenderHtml")
			{
				var buildFunction = await base.Build(step);

				if (buildFunction.BuilderError != null) return (null, buildFunction.BuilderError);
				return buildFunction;
			}
			*/
			var result = await base.Build<GenericFunction>(step);
			if (result.BuilderError != null) return (null, result.BuilderError);

			var function = result.Instruction.Function;
			
			return result;
		}

		public async Task<(Instruction?, IBuilderError?)> BuilderRenderTemplate(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			
			var renderOption = gf.GetParameter<RenderTemplateOptions>("options");
			if (renderOption.RenderMessage == null)
			{
				return (instruction, new StepBuilderError("RenderMessage needs to be defined", step));
			}
			if (string.IsNullOrEmpty(renderOption.RenderMessage.Content))
			{
				return (instruction, new StepBuilderError("FileName or html needs to be defined", step));
			}

			if (PathHelper.IsTemplateFile(renderOption.RenderMessage.Content))
			{
				var filePath = GetPath(renderOption.RenderMessage.Content, step.Goal);
				if (!fileSystem.File.Exists(filePath) && !filePath.Contains("%"))
				{
					Dictionary<string, object?> parameters = new();
					parameters.Add("step", step);
					parameters.Add("goal", step.Goal);
					parameters.Add("instruction", instruction);
					parameters.Add("fileName", renderOption.RenderMessage.Content);

					GoalToCallInfo goalToCallInfo = new GoalToCallInfo("/modules/UiModule/CreateTemplateFile", parameters);

					var program = engine.Modules.Get<CallGoalModule.Program>().Module!;
					var result = await program.RunGoal(goalToCallInfo);
					if (result.Error != null) return (instruction, new BuilderError(result.Error));
				}
			}
			
			var events = gf.GetParameter<List<Event>?>("events");
			if (events == null || events.Count == 0) return (instruction, null);

			/*
			

			if (renderOption?.FileNameOrHtml.Contains("%") == true)
			{
				return (instruction, new StepBuilderError($"Events cannot be added to dynamic file path {renderOption.FileNameOrHtml}", step));
			}

			List<LlmMessage> messages = new();
			messages.Add(new LlmMessage("system", @$"
I have this scriban template, I want you to modify ONLY what I request.

I want to add js event on css selector or scriban {{ variable }} from the list of <events>

%variables% should match with the scriban variables, e.g. {{ item.productId }} => %productId%
call XXX calls the javascript function plang.post(name, parameters), this function is already defined
parameters is a key:value object, e.g. {{ productId: {{{{ item.productId }}}} }}
you need to understand the structure of html and variables, as they might be referenced, e.g. user might say %productId% when the variable in scriban is {{{{ item.productId }}}} 

<events>
{JsonConvert.SerializeObject(events)}
</events>


EventType: event type bind to an object
CssSelectorOrVariable: is either a scriban {{{{ variable }}}} or a css selector
GoalToCall: defines the path and parameters to call

Examples:
{{""EventType"":""onchange"",""CssSelectorOrVariable"":""{{{{ item.quantity }}}}"",""GoalToCall"":{{""Name"":""UpdateQuantity"",""Parameters"":{{""variantId"":""%variantId%"",""quantity"":""%quantity%""}}}}

<input type=""number"" name=""qty"" value=""{{{{ item.quantity }}}}""> becomes <input type=""number"" name=""qty"" value=""{{{{ item.quantity }}}}"" onchange=""plang.post('UpdateQuantity', {{variantId: {{{{ item.variantId }}}}, quantity: this.value}});"">

"));

			var templatePath = PathHelper.GetPath(renderOption.FileNameOrHtml, fileSystem, step.Goal);
			var html = fileSystem.File.ReadAllText(templatePath);
			messages.Add(new LlmMessage("user", html));

			var llmRequest = new LlmRequest("BuilderRenderTemplate", messages);
			llmRequest.llmResponseType = "html";


			var llm = llmServiceFactory.CreateHandler();

			var result = await llm.Query<string>(llmRequest);
			if (result.Error != null) return (null, new BuilderError(result.Error));

			fileSystem.File.WriteAllText(templatePath + "2", html);
			fileSystem.File.WriteAllText(templatePath, result.Response);

			return (instruction, null);

		}



		public async Task<(Instruction?, IBuilderError?)> BuildRenderHtml(GoalStep step) { 
				var nextStep = step.NextStep;

			List<string> subElements = new();
			while (nextStep != null && nextStep.Indent == step.Indent + 4)
			{
				subElements.Add(nextStep.Text);
				nextStep = nextStep.NextStep;
			}



			string childElementsSystem = "";
			if (subElements.Count > 0)
			{
				childElementsSystem = $@"## ChildElement rules #
DO NOT generate html for Children Elements

Children are elements that are child element in the dom of the user input
Insert {{{{ ChildElement0 }}}}, {{{{ ChildElement1 }}}}, {{{{ ChildElementN }}}}, etc. where the ChildElement's fit in your generated html
## ChildElement rules ##
## Current child elements ##:
- {string.Join("\n- ", subElements)}
## Current child elements ##
";
			}

			string scribanExamples = $@"### Scriban examples ###
FROM user command, generate using Scriban

Variables in plural are lists, singular is object. 

{{{{ for product in products }}}}
    <li>
      <h2>{{{{ product.name }}}}</h2>
           Price: {{{{ product.price }}}}
           {{{{ product.description | string.truncate 15 }}}}
    </li>
  {{{{ end }}

<h3>{{{{book.Title}}}}</h3>

{{{{ var isUserLoggedIn = isLoggedIn }}}}

{{{{ if isUserLoggedIn }}}}
    <p>Welcome back, {{{{ user.Username }}}}</p>
{{{{ else }}}}
    <p>Please log in.</p>
{{{{ end }}}}
### Scriban examples ###";
			var variables = GetVariablesInStep(step);

			SetSystem(@$"You are a code generator specialist generating valid, strict and to the book code with nice looking GUI from Plang programming language. 
Create the html, javascript and css from the user intent using vanilla javascript, UIkit 3.15.10 and Scriban template engine
{childElementsSystem}
## Plang Rules ##
User is programming in a language called Plang
User will provide one step of many to construct the GUI
Variables are defined with starting and ending %. They are case sensitive so keep them as defined
## Plang Rules ##

## Code generation rules ##

Convert plang variables to Scriban variables, e.g. %variableName% = {{ variableName }}
Goals are prefixed with !, they are for calling a method, e.g. Call !NewUser or reference a goal, such as Edit.goal, the goal is the href or action. 
DO NOT generate code for child or parent elements, this will be done by different system and it is NOT appropriate to generate that html/css/js code as it will crash the system 
What you generate is final and most be strict, this mean:
	- DO NOT assume any extra elements, such as menu structure, edit/delete button
	- DO NOT assume variable(s) or property on variable(s) that are not defined by user intent
## Code generation rules ##

## uikit ##
when user defines things like article, accordion, dropbar and other elements provided by UIkit. Use UIkit suggested format

UIkit format examples:
	<label class=""uk-form-label"">
	<button class=""uk-button uk-button-XXX""> XXX: default|primary|secondary 
## uikit ##

## html rules ##
Start of html document(e.g. Doctype, <html>, <body>) will be provided by different system 
link and script tags to files are always available locally and should be prefixed with local://
local resources should be prefix with local://, e.g. src=""image.png"" => src=""local://image.png""
HTML comments and <!-- --> are NOT allowed
follow good aria standards
Only generate html that is described in user intent AND you do not assume elements that not defined, for example dont generate submit button for form unless defined by user input
DO NOT generate html for ChildElement.
DO NOT wrap ChildElement in div.
All form elements must have name attribute, use plang %variable% name when available
Use UIkit class names
set #id to element when defined by user, e.g. `- input #name ...` => <input id=""name""...
User input MUST be wrapped in block element when elements are inline, unless defined by user not to have it block element.
%variable% used on user input MUST be added to element, example: input %name% ... => <input data-plang-var=""name"" value=""{{{{ name }}}}"" ...>, article text => <article data-plang-var=""text"" class=""uk-article"">{{{{ text }}}}</article>
when user defines conditions or loops try to solve with Scriban 
when user defines a call, it goes through await plangUi.callGoal(event.target, 'GoalName', parameters?:object)
when user wants to delay(debounce), add attibute plang-delay=""delayInMilliseconds"", plangUi will handle it.
when user wants to confirm, set attribute on element to plang-confirm=""{{json}}"" - see ## plang-attributes rules ## for json. System provides js for plang-confirm
## html rules ##

## plang-attributes rules ##
plang-confirm: {{ ""message"" :string, {{ ""i18n"": {{""ok"": string, ""cancel"": string }} }} 

## plang-attributes rules ##

## css rules ##
Only generate css if user defines styling in his intent
Css should be using up to date css standards. colors should be in rgb.
Css file being referenced can only be local and must be prefixed with local://, <link href=""style.css""> => <link href=""local://style.css"">
## css rules ##

## javascript rules ##
Only generate javascript when needed or when user defines it, try to solve with Scriban 
Javascript should be vanilla Javascript.
javascript file being referenced can only be local and must be prefixed with local://, <script src=""code.js""> => <script src=""local://code.js"">
All object.Id or object.id are long and needs to be wrapped with single quote(').
Always generate fully implemented javascript code
## javascript rules ##

### Scriban rules ##
Keep to valid Scriban script
use date_format instead of date.to_string or date.format => {{ created | date_format ""....."" }} where ..... is the c# datetime format.
Respect culture for date_format, when user defines to be formatted by date(""d"") or time(""t""), only force format when user defines it
### Scriban rules ##
### Scriban examples ### 
{scribanExamples}
### Scriban examples ###

### plang examples of user input and llm response ###
user input: - #main content 
   child elements:   - input name=age
-input=address => html: <div class=""uk-container"">{{{{ ChildElement0 }}}}
{{{{ ChildElement1 }}}}</div>
user input: - html => html: <html>
user input:- article %user% => html: <article class=""uk-article"">{{{{ user }}}}</article>
user input: form that send to Save
	child elements: - input name
					- submit button 
	=> html: ""<form action=""Save"" method=""post"">
{{{{ ChildElement0 }}}}
{{{{ ChildElement1 }}}}
</form>""
user input: search, call Search => html: ""<input type=""search"" oninput=""plangUi.callGoal(this, 'Search', {{ search: this.value }})"" />
user input: button, call Get %id% => html: ""<button onclick=""plangUi.callGoal(this, 'Get', {{ id: '{{{{ id }}}}' }})"" />
### plang examples of user input and llm response ###

## code_gen_planrules ##
describe user intent and create a plan for it
stick to user intent and DO NOT assume elements not described, for example DO NOT create form element, buttons, etc. that are not in user intent 
## code_gen_plan rules ##
");
			SetAssistant($@"### variables available ###
{variables}
### variables available ###");

			
			

			var build = await base.Build<UiResponse>(step);
			if (build.BuilderError != null || build.Instruction == null)
			{
				return (null, build.BuilderError ?? new StepBuilderError("Could not build step", step));
			}

			List<string> missingChildren = new();
			var uiResponse = build.Instruction.Function as UiResponse;

			if (!string.IsNullOrEmpty(uiResponse.html) && !uiResponse.html.Contains("<"))
			{
				
				var error = new BuilderError($"You didn't response with valid html. Your previous response was: {uiResponse.html}");

				logger.LogWarning("Html didn't contain child elements, asking LLM again");
				return await Build(step, error);
			}

			for (var i = 0; i < subElements.Count; i++)
			{
				if (uiResponse == null || string.IsNullOrEmpty(uiResponse.html))
				{
					//rebuild
				}
				if (!uiResponse.html.Contains($"{{{{ ChildElement{i} }}}}"))
				{
					missingChildren.Add($"{{{{ ChildElement{i} }}}}");

					//rebuild
				}
			}

			if (missingChildren.Count > 0)
			{
				var error = new BuilderError($"There is missing {{{{ ChildElementN }}}} in your response, there should be {missingChildren.Count} child elements. Your previous response was: {uiResponse.html}");

				logger.LogWarning("Html didn't contain child elements, asking LLM again");
				return await Build(step, error);
			}


			List<Parameter> parameters = new List<Parameter>();


			if (uiResponse.html != null) parameters.Add(new Parameter("string", "html", uiResponse.html));
			if (uiResponse.css != null) parameters.Add(new Parameter("string", "css", uiResponse.css));
			if (uiResponse.javascript != null) parameters.Add(new Parameter("string", "javascript", uiResponse.javascript));

			var gf = new GenericFunction("", "RenderHtml", parameters, null);


			var instruction = InstructionCreator.Create(gf, step, build.Instruction.LlmRequest);
			return (instruction, null);
		}

		public async Task<(Instruction, IBuilderError?)> BuilderSetFrameworks(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			var caller = engine.Modules.Get<CallGoalModule.Program>().Module!;

			var framework = gf.GetParameter<UiFramework>("framework");
			var dict = new Dictionary<string, object?>();
			dict.Add("framework", memoryStack.LoadVariables(framework));

			var goalToCall = new GoalToCallInfo("/modules/UiModule/Builder/SetFrameworks", dict);

			var result = await caller.RunGoal(goalToCall);
			if (result.Error != null) return (instruction, new BuilderError(result.Error));

			framework = result.Return as UiFramework;

			var variable = engine.Modules.Get<VariableModule.Program>().Module!; 
			var storedFrameworks = await variable.GetSettings<UiFramework>("UiFrameworks");

			await variable.SetSettingValue("UiFrameworks", storedFrameworks);

			return (instruction, null);
		}
		public async Task<(Instruction, IBuilderError?)> BuilderSetLayout(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			/*
			var caller = engine.Modules.Get<CallGoalModule.Program>().Module!;

			var options = gf.GetParameter<LayoutOptions>("options");
			var dict = new Dictionary<string, object?>();
			dict.Add("options", memoryStack.LoadVariables(options));

			var goalToCall = new GoalToCallInfo("/modules/UiModule/Builder/SetLayout", dict);

			var result = await caller.RunGoal(goalToCall);
			if (result.Error != null) return (instruction, new BuilderError(result.Error));
			*/
			return (instruction, null);
		}
	}

	public record UiResponse(string? html = null, string? javascript = null, string? css = null);
}

