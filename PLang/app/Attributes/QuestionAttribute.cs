namespace app.Attributes;

/// <summary>
/// This action is a question: its answer is the verdict an <c>if</c> tests (<c>file.exists</c>, <c>list.contains</c>).
/// On a step whose lone if it is asked of, it stands before the if, which reads its answer
/// (<c>file.exists(Path); condition.if(Left) { … }</c>). An action that does a thing and answers where it did it
/// (<c>file.save</c>) is no question, whatever its answer's truth. The builder asks the action (<c>action.IsQuestion</c>),
/// never the handler.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
public sealed class QuestionAttribute : System.Attribute
{
}
