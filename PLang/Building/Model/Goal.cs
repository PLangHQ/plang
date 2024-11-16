using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace PLang.Building.Model;
/*
public class GoalFile
{
    public GoalFile()
    {
    }
    public GoalFile(string fileName)
    {
        this.FileName = fileName;
        FileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        Goals = new List<Goal>();
    }

    public string FileName { get; set; }
    public string FileNameWithoutExtension { get; set; }
    public List<Goal> Goals { get; set; }
    public string Assistant { get; set; }


}*/

public enum Visibility
{
    Private = 0,
    Public = 1
}

public class Goal
{
    public Goal()
    {
        GoalSteps = new List<GoalStep>();
        Injections = new List<Injections>();
        SubGoals = new List<string>();
    }

    public Goal(string goalName, List<GoalStep> steps)
    {
        GoalName = goalName;
        GoalSteps = steps;
        GoalSteps = new List<GoalStep>();
        Injections = new List<Injections>();
        SubGoals = new List<string>();
    }

    public string GoalName { get; set; }
    public string? Comment { get; set; }
    public string Text { get; set; }
    public List<GoalStep> GoalSteps { get; set; }
    public List<string> SubGoals { get; set; }
    public string? Description { get; set; }
    public Visibility Visibility { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AppName { get; set; }

    public string GoalFileName { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string PrFileName { get; set; }

    public string RelativeGoalPath { get; set; }
    public string RelativeGoalFolderPath { get; set; }
    public string RelativePrPath { get; set; }
    public string RelativePrFolderPath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AbsoluteGoalPath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AbsoluteGoalFolderPath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AbsolutePrFilePath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AbsolutePrFolderPath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AbsoluteAppStartupFolderPath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string RelativeAppStartupFolderPath { get; set; }

    public string BuilderVersion { get; set; }
    public GoalInfo GoalInfo { get; set; }

    public List<Injections> Injections { get; set; }

    //Signature should be used when goal is deployed
    //this allows for validating the publisher and that code has not changed.
    public string Signature { get; set; }
    public string Hash { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public Goal? ParentGoal { get; set; }

    public string[] IncomingVariablesRequired { get; set; }

    public string GetGoalAsString()
    {
        var goal = "";
        if (!string.IsNullOrWhiteSpace(Comment)) goal = $"/ {Comment}\n";
        goal += GoalName + "\n";
        foreach (var step in GoalSteps)
        {
            if (!string.IsNullOrWhiteSpace(step.Comment)) goal += $"/ {step.Comment}\n";
            goal += "- ".PadLeft(step.Indent, ' ') + step.Text + "\n";
        }

        return goal;
    }
}