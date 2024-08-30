using OpenQA.Selenium.DevTools.V124.DOM;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Xml;

namespace PLang.Modules.UiModule

{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step)
		{
			/*var build = await base.Build(step);
			if (build.BuilderError != null) return (null, build.BuilderError);

			if (build.Instruction?.GetFunctions().FirstOrDefault()?.FunctionName != "RenderHtml") return build;
			

			bool children = false;
			string childrenStr = "";
			string str = $"(Goal) {step.Goal.GoalName}\n";
			for (int i = 0; i < step.Goal.GoalSteps.Count; i++)
			{
				str += $"- (%step{step.Goal.GoalSteps[i].Number}%) {step.Goal.GoalSteps[i].Text}\n".PadLeft(step.Goal.GoalSteps[i].Indent, ' ');
				if (step.Goal.GoalSteps[i].Text == step.Text) children = true;
				if (children && step.Indent < step.Goal.GoalSteps[i].Indent)
				{
					childrenStr += $"{{step{step.Goal.GoalSteps[i].Number}}}\n";
				}
				else if (step.Goal.GoalSteps[i].Text != step.Text)
				{
					children = false;
				}
			}
			*/

			var nextStep = step.NextStep;

			List<string> subElements = new();
			while (nextStep != null && nextStep.Indent == step.Indent + 4)
			{
				subElements.Add(nextStep.Text);
				nextStep = nextStep.NextStep;
			}



			string childElementsSystem = "";
			string strChildElements = "";
			if (subElements.Count > 0)
			{
				strChildElements = $@"## ChildrenElements ##
{string.Join("\n", subElements)}
## Children ##";
				childElementsSystem = $@"Children are elements that are child element in the dom of the user input
Insert {{{{ ChildrenElement0 }}}}, {{{{ ChildrenElement1 }}}}, {{{{ ChildrenElementN }}}}, etc. where the ChildrenElement fit in your generated html";
			}

			string scribanExamples = $@"### Scriban examples ###
FROM user command, generate using Razor

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
Create the html, javascript and css from the user intent using htmx 1.9.2, uikit 3.15.10 and Scriban template engine

## Plang Rules ##
User is programming in a language called Plang
User will provide one step of many to construct the GUI
Only give your response on this specific step
Steps start with dash(-)
Variables are defined with starting and ending %. They are case sensitive so keep them as defined
## Plang Rules ##

## Code generation rules ##
Convert plang variables to Scriban variables, e.g. %variableName% = {{ variableName }}
Goals are prefixed with !, they are for calling a method, e.g. Call !NewUser or reference a goal, such as Edit.goal. To call it use javascript function callGoal(name:string, parameters:object). Send parameters to callGoal that make sense
DO NOT generate code for child elements, this will be done by different system and it is NOT appropriate to generate that html/css/js code as it will crash the system
What you generate is final and most be strict, this mean:
	- DO NOT assume any extra elements, such as menu structure, edit/delete button
	- DO NOT assume variable(s) or property on variable(s) that are not defined by user intent
## Code generation rules ##
## uikit ##
when user defines things like article, accordion, dropbar and other elements provided by UIkit. Use their format
## uikit ##

## html rules ##
Start of html document(e.g. Doctype, <html>, <body>) will be provided by different system 
<a> should use hx- htmx ajax, external links should open in target=""_blank""
link and script tags to files are always available locally and should be prefixed with local://
local resources should be prefix with local://, e.g. src=""image.png"" => src=""local://image.png""
HTML comments and <!-- --> are NOT allowed
follow good aria standards
Only generate html that is described in user intent AND you do not assume elements that not defined, for example dont generate submit button for form unless defined
DO NOT generate html for ChildrenElements.
All form elements must have name attribute, use plang variable name when available
## html rules ##

## css rules ##
Only generate css if user defines styling in his intent
Css should be using up to date css standards. colors should be in rgb.
Css file being referenced can only be local and must be prefixed with local://, <link href=""style.css""> => <link href=""local://style.css"">
## css rules ##

## javascript rules ##
Only generate javascript when needed or when user defines it
Javascript should be vanilla Javascript.
javascript file being referenced can only be local and must be prefixed with local://, <script src=""code.js""> => <script src=""local://code.js"">
DO NOT generate the function callGoal, it will be provided
All object.Id or object.id are long and needs to be wrapped with single quote(').
Always generate fully implemented javascript code
## javascript rules ##

{strChildElements}
{scribanExamples}

### plang examples ###
- #main content 
     - call goal Home => html: <div class=""uk-container"">{{{{ ChildrenElements0 }}}}</div>
- html => html: <html>
- article %user% => html: <article class=\""uk-article\"">{{{{ user }}}}</article>
### plang examples ###
");
			SetAssistant($@"### variables available ###
{variables}
### variables available ###");


			var build = await base.Build<UiResponse>(step);
			if (build.BuilderError != null || build.Instruction == null)
			{
				return (null, build.BuilderError ?? new StepBuilderError("Could not build step", step));
			}

			var uiResponse = build.Instruction.Action as UiResponse;
			for (var i=0;i< subElements.Count;i++)
			{
				if (uiResponse == null || string.IsNullOrEmpty(uiResponse.html)) {
					//rebuild
				}
				if (!uiResponse.html.Contains($"{{{{ ChildrenElement{i} }}}}"))
				{
					int x = 0;
					//rebuild
				}
			}


			List<Parameter> parameters = new List<Parameter>();

			var wrapper = new PlangVarHtmlWrapper();
			//string? html = wrapper.WrapHtml(uiResponse.html);

			if (uiResponse.html != null) parameters.Add(new Parameter("string", "html", uiResponse.html));
			if (uiResponse.css != null) parameters.Add(new Parameter("string", "css", uiResponse.css));
			if (uiResponse.javascript != null) parameters.Add(new Parameter("string", "javascript", uiResponse.javascript));

			var gf = new GenericFunction("RenderHtml", parameters, null);


			var instruction = new Instruction(gf);
			instruction.LlmRequest = build.Instruction.LlmRequest;
			return (instruction, null);
		}

		
	}

	public record UiResponse(string? html = null, string? javascript = null, string? css = null);
}

