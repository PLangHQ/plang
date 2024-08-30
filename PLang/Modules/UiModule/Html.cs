using HtmlAgilityPack;
using Scriban;
using Scriban.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace PLang.Modules.UiModule
{
	public class PlangVarHtmlWrapper
	{
		static void ProcessForLoops(HtmlNode bodyNode)
		{
			var forPattern = @"\{\{\s*for\s+(\w+)\s+in\s+[^\}]+\s*\}\}";
			var endPattern = @"\{\{\s*end\s*\}\}";

			var forMatches = Regex.Matches(bodyNode.InnerHtml, forPattern);
			foreach (Match forMatch in forMatches)
			{
				string forLoop = forMatch.Value;
				string loopVariable = forMatch.Groups[1].Value;
				string modifiedInnerHtml = bodyNode.InnerHtml.Replace(forLoop, $"<plang_var name=\"{loopVariable}s\">\n{forLoop}");

				var endMatch = Regex.Match(modifiedInnerHtml, endPattern);
				if (endMatch.Success)
				{
					string endLoop = endMatch.Value;
					modifiedInnerHtml = modifiedInnerHtml.Replace(endLoop, $"{endLoop}\n</plang_var>");
				}

				bodyNode.InnerHtml = modifiedInnerHtml;

				string variablePattern = @"\{\{\s*[^{}]+\s*\}\}";
				modifiedInnerHtml = bodyNode.InnerHtml;

				var variableMatches = Regex.Matches(modifiedInnerHtml, variablePattern);
				foreach (Match variableMatch in variableMatches)
				{
					string variable = variableMatch.Value;

					if (!variable.Contains("for") && !variable.Contains("end") && !variable.Contains(loopVariable + "."))
					{
						string variableName = GetVariableName(variable);
						string wrappedVariable = $"<plang_var name=\"{variableName}\">{variable}</plang_var>";
						modifiedInnerHtml = modifiedInnerHtml.Replace(variable, wrappedVariable);
					}
				}

				bodyNode.InnerHtml = modifiedInnerHtml;
			}
		}

		static void ProcessStandaloneVariables(HtmlNode bodyNode)
		{
			string variablePattern = @"\{\{\s*[^{}]+\s*\}\}";
			var variableMatches = Regex.Matches(bodyNode.InnerHtml, variablePattern);
			foreach (Match variableMatch in variableMatches)
			{
				string variable = variableMatch.Value;
				string variableName = GetVariableName(variable);

				// Skip wrapping ChildElement[0-9]+ variables
				if (!Regex.IsMatch(variableName, @"^ChildElement\d+$"))
				{
					string wrappedVariable = $"<plang_var name=\"{variableName}\">{variable}</plang_var>";
					bodyNode.InnerHtml = bodyNode.InnerHtml.Replace(variable, wrappedVariable);
				}
			}
		}


		public string? WrapHtml(string? html)
		{
			if (html == null) return null;
			// Parse the template into a Scriban template object
			var template = Template.Parse(html);
			// Modify the template using the syntax tree
			var modifiedTemplate = new StringBuilder();
			ModifyTemplate(template.Page.Body, modifiedTemplate);

			return modifiedTemplate.ToString();

		}

		static void ModifyTemplate(ScriptBlockStatement block, StringBuilder output, ScriptExpression? scriptExpression = null)
		{
			foreach (var statement in block.Statements)
			{
				if (statement is ScriptForStatement forStatement)
				{
					// Handle for-loops by wrapping them with <plang_var>
					string forLoopHeader = forStatement.ToString().Split('\n')[0].Trim(); // Only the first line ({{ for ... in ... }})
					output.AppendLine($"<plang_var name=\"{forStatement.Variable}\">");
					output.Append("{{ " + forLoopHeader);
					ModifyTemplate(forStatement.Body, output, forStatement.Variable); // Recursively handle the body of the loop
					output.Append("\n</plang_var>");
				}
				else if (statement is ScriptIfStatement)
				{
					output.Append($"{{{{{statement}}}}}");
				}
				else if (statement is ScriptExpressionStatement expressionStatement)
				{
					if (statement.ToString().StartsWith("ChildrenElement"))
					{
						output.Append($"{{{{ {statement} }}}}");
					} else if (expressionStatement.Expression is ScriptVariableGlobal globalVariable)
					{
						if (scriptExpression == null || !globalVariable.Name.ToString().Contains(scriptExpression.ToString() + "."))
						{
							// Wrap global variables
							output.Append($"<plang_var name=\"{globalVariable.Name}\">{{{{ {globalVariable.Name} }}}}</plang_var>");
						}
						else if (scriptExpression != null)
						{
							output.Append($"{{{{ {globalVariable.Name} }}}}");
						}
					}
					else if (expressionStatement.Expression is IScriptVariablePath path)
					{
						if (scriptExpression == null || !path.ToString().Contains(scriptExpression.ToString() + "."))
						{
							// Wrap variable paths like object.property
							output.Append($"<plang_var name=\"{path}\">{{{{ {path} }}}}</plang_var>");
						}
						else if (scriptExpression != null)
						{
							output.Append($"{{{{ {path} }}}}");
						}
					}
					else if (expressionStatement.Expression is ScriptBinaryExpression binaryExpression)
					{
						// Handle binary expressions (e.g., filters or operations)
						string expression = binaryExpression.ToString();
						string wrappedExpression = $"<plang_var name=\"expression\">{{{{ {expression} }}}}</plang_var>";
						output.Append(wrappedExpression);
					}
					else
					{
						// Default case: just append the expression
						output.Append(statement.ToString());
					}
				}
				else if (statement is ScriptEscapeStatement)
				{
				} else if (statement is ScriptEndStatement)
				{
					output.Append($"{{{{ {statement} }}}}");

				} else { 
						// Default case: handle other types of statements as is
						output.Append(statement.ToString());
					
				}
			}
		}

		static string GetVariableName(string scribanVariable)
		{
			var match = Regex.Match(scribanVariable, @"\{\{\s*([\w.]+)");
			return match.Success ? match.Groups[1].Value.Split('.').Last() : "unknown";
		}
	}



}
