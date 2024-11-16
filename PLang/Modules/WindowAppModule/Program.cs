using System.ComponentModel;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;

namespace PLang.Modules.WindowAppModule;

public class Program : BaseProgram
{
    private readonly IEngine engine;
    private readonly IOutputStreamFactory outputStreamFactory;
    private readonly IPseudoRuntime pseudoRuntime;

    public Program(IPseudoRuntime pseudoRuntime, IEngine engine, IOutputStreamFactory outputStreamFactory)
    {
        this.outputStreamFactory = outputStreamFactory;
        this.pseudoRuntime = pseudoRuntime;
        this.engine = engine;
    }

    [Description(
        "goalName is required. It is one word. Example: call !NameOfGoal, run !Google.Search. Do not use the names in your response unless defined by user")]
    public async Task<IError?> RunWindowApp(GoalToCall goalName, Dictionary<string, object?>? parameters = null,
        int width = 800, int height = 450, string? iconPath = null, string windowTitle = "plang")
    {
        var outputStream = outputStreamFactory.CreateHandler();
        if (outputStream is not UIOutputStream os)
            return new StepError("This is not UI Output, did you run plang instead of plangw?", goalStep);

        os.IForm.SetTitle(windowTitle);
        os.IForm.SetSize(width, height);
        if (iconPath != null) os.IForm.SetIcon(iconPath);

        var result = await pseudoRuntime.RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), goalName,
            parameters, Goal);

        os.IForm.Visible = true;
        return result.error;
    }
}