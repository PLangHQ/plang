using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Utils;
using PLang.Utils.Extractors;

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
			while (nextStep.Indent == step.Indent + 4)
			{
				subElements.Add(nextStep.Text);
				nextStep = nextStep.NextStep;
			}
			


			string strSubElements = "";
			if (subElements.Count > 0)
			{
				strSubElements = $@"## SubElements ##
{string.Join("\n", subElements)}
## SubElements ##";
			}

			string scribanExamples = $@"### Scriban examples ###
FROM user command, generate using Razor

Variables in plural are lists, singular is object. 

{{ for product in products }}
    <li>
      <h2>{{ product.name }}</h2>
           Price: {{ product.price }}
           {{ product.description | string.truncate 15 }}
    </li>
  {{ end }}

<h3>{{book.Title}}</h3>

{{ var isUserLoggedIn = isLoggedIn }}

{{ if isUserLoggedIn }}
    <p>Welcome back, {{ user.Username }}!</p>
{{ else }}
    <p>Please log in.</p>
{{ end }}
### Scriban examples ###";
			var variables = GetVariablesInStep(step);

			SetSystem(@$"You are a htmx, uikit and Scriban specialist generating nice looking GUI from Plang programming language.

## Rules ##
User will provide one step of many to construct the gui
Only focus on this specific step
Steps start with dash(-)
Variables are defined with starting and ending %. They are case sensitive so keep them as defined
Goals are prefixed with !, they are for calling a method, e.g. Call !NewUser or reference a goal, such as Edit.goal. To call it use javascript function callGoal(name:string, parameters:object)
DO NOT generate the function callGoal, it will be provided
All object.Id or object.id are long and needs to be wrapped with single quote(').
Start of html document(e.g. Doctype, <html>, <body>) will be provided by different system 
HTML comments and <!-- --> are NOT allowed
Javascript should be vanilla Javascript. 
Css should be using up to date css standards. colors should be in rgb.
If a feedback is needed from the user using the html, provide the solution also for that, if it needs javascript, provide javascript, if it needs custom css, provide css
ChildElements are elements that are child element in the dom of the user input
Insert <placeholder1>, <placeholder2>, <placeholderN>, etc. for SubElements. Do not generate code for it.
Links should use xhtm ajax and target #main as default target unless defined by user
## Rules ##

{strSubElements}

### variables available ###
{variables}
### variables available ###

{scribanExamples}

Your job is to build ```html, ```javascript, ```css from the user command and nothing else.
");



			var build = await base.Build<UiResponse>(step);
			if (build.BuilderError != null || build.Instruction == null)
			{
				return (null, build.BuilderError ?? new StepBuilderError("Could not build step", step));
			}

			var uiResponse = build.Instruction.Action as UiResponse;
			List<Parameter> parameters = new List<Parameter>();
			if (uiResponse.html != null) parameters.Add(new Parameter("string", "html", uiResponse.html));
			if (uiResponse.css != null) parameters.Add(new Parameter("string", "css", uiResponse.css));
			if (uiResponse.javascript != null) parameters.Add(new Parameter("string", "javascript", uiResponse.javascript));
			//if (uiResponse.javascriptFunctionToCall != null) parameters.Add(new Parameter("string", "javascriptFunctionToCall", uiResponse.javascriptFunctionToCall));

			var gf = new GenericFunction("RenderHtml", parameters, null);


			var instruction = new Instruction(gf);
			step.Execute = true;
			instruction.LlmRequest = build.Instruction.LlmRequest;
			return (instruction, null);
		}


	}

	public record UiResponse(string? html = null, string? javascript = null, string? css = null);
}

