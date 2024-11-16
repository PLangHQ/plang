using static PLang.Modules.UiModule.Program;

namespace PLang.Interfaces;

public interface IForm
{
    public SynchronizationContext SynchronizationContext { get; set; }
    bool Visible { get; set; }
    void SetSize(int width, int height);
    void SetIcon(string? icon);
    void SetTitle(string? title);
    Task ModifyContent(string content, OutputTarget outputTarget, string id);
    Task ExecuteCode(string content);
    Task Flush();
}