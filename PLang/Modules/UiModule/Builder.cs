using PLang.Building.Model;

using PLang.Utils;
using PLang.Utils.Extractors;

namespace PLang.Modules.UiModule

{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<Instruction> Build(GoalStep step)
		{
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

			string contentToBeAdded = "";
			if (childrenStr != "")
			{
				contentToBeAdded = $"Put the variable {{step#}} where sub steps should be. e.g. if you generated html to insert to step it would look something like this. <div><div>{{step1}}</div><div>{{step2}}</div></div>";
			}


			SetSystem(@$"Create the html, javascript and css needed from the user command using Bootstrap 5.0.2 & Fontawesome 5.15.3. 

Goal has series of steps. Steps start with dash(-), steps can have sub steps, substeps are indented and are referenced in previous step by {{step#}} where # is a number, e.g. {{step1}}.
Variables are defined with starting and ending %. They are case sensitive so keep them as defined
Goals are prefixed with !, they are for calling a method, e.g. Call !NewUser or reference a goal, such as Edit.goal. To call it use javascript function callGoal(name:string, parameters:object)
DO NOT generate the function callGoal, it will be provided
All object.Id or object.id are long and needs to be wrapped with single quote('). This does not apply to other id properties e.g. object.object_id
All properties on variables are case sensitive, keep formatting defined by user.

{contentToBeAdded}  

HTML comments and <!-- --> are NOT allowed

Javascript should be vanilla Javascript. popper.min.js and bootstrap.min.js are available. 

Use @Razor templating engine for variables and to go through lists, displaying object or property, see example later

Css should be using up to date css standards. colors should be in rgb.

If a feedback is needed from the user using the html, provide the solution also for that, if it needs javascript, provide javascript, if it needs custom css, provide css

Goalfile is only provide for context. 

Your job is to build ```html, ```javascript, ```css from the user command and nothing else.
");

			var variables = GetVariablesInStep(step);
			SetAssistant($@"
### variables available ###
{variables}
### variables available ###
### Goalfile ###
{str}
### Goalfile ###
### Razor ###
Variables in plural are lists, singular is object. 
To access variable, prefix it with Model.
use the variable name as the name for the list and when looping through the list. 
Example for a tr in a table.

@foreach (var task in Model.tasks)
{{
    <tr>
        <td>@task.Description</td>
        <td>Due date: @task.DueDate</td>
		<td><a href=""javascript:callGoal('edit.goal', {{id:'@task.Id'}})"">Edit</a></td>
		<td><button onclick=""callGoal('delete.goal', {{id:'@task.Id'}})"">Delete</button></td>
    </tr>
}}

<h3>@Model.book.Title</h3>

@{{
    bool isUserLoggedIn = Model.isLoggedIn;
}}

@if (isUserLoggedIn)
{{
    <p>Welcome back, @user.Username!</p>
}}
else
{{
    <p>Please log in.</p>
}}
### Razor ###");

			var result = await base.Build<UiResponse>(step);

			var uiResponse = result.Action as UiResponse;
			List<Parameter> parameters = new List<Parameter>();
			parameters.Add(new Parameter("string", "html", uiResponse.html));
			parameters.Add(new Parameter("string", "css", uiResponse.css));
			parameters.Add(new Parameter("string", "javascript", uiResponse.javascript));

			var gf = new GenericFunction("RenderHtml", parameters, null);


			var instruction = new Instruction(gf);
			step.Execute = true;
			instruction.LlmRequest = result.LlmRequest;
			return instruction;
		}




	}

	public record UiResponse(string html, string javascript, string css);
}

