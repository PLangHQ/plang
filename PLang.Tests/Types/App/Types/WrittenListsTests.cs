namespace PLang.Tests.App.Types;

// The generator lists, at compile time, the classes plang's facts read (PLang.Generated.Written), so a cold start reads
// lists instead of walking every class. Each list must hold every class the facts' own rules would pick when walking:
// a class missing from a list is a type, format, set, action, reader or pair the app would silently not have.
public class WrittenListsTests
{
    private static readonly System.Reflection.Assembly Plang = typeof(global::app.@this).Assembly;

    // every class the code wrote — the compiler's own (a closure, a state machine) are no fact's
    private static System.Type[] Written() => Plang.GetTypes().Where(clr => !clr.Name.Contains('<')).ToArray();

    private static System.Type[] Listed(string name)
        => (System.Type[])Plang.GetType("PLang.Generated.Written")!
            .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;

    private static async Task Holds(string list, System.Func<System.Type, bool> picks)
    {
        var listed = Listed(list).ToHashSet();
        var missing = Written().Where(picks).Where(clr => !listed.Contains(clr)).Select(clr => clr.FullName).ToList();
        await Assert.That(missing).IsEmpty().Because($"the generator's {list} list must hold every class the facts would pick");
    }

    // what the facts take from the item list: a class that is a type, a kind class, a family's subclass, or one whose
    // declared word names its folder's reader
    [Test] public Task TheItemList_HoldsEveryItemClassAFactTakes()
        => Holds("Item", clr => typeof(global::app.type.item.@this).IsAssignableFrom(clr)
            && (global::app.type.list.@this.NameOf(clr) != null
                || typeof(global::app.type.kind.@this).IsAssignableFrom(clr)
                || global::app.type.item.@this.FamilyOf(clr) != null
                || global::app.type.item.@this.WordOf(clr) != null));

    [Test] public Task TheFormatList_HoldsEveryClassDeclaringAFormat()
        => Holds("Format", clr => clr.IsDefined(typeof(global::app.Attributes.FormatAttribute), inherit: false));

    [Test] public Task TheChoiceList_HoldsEveryClassMarkedAChoice()
        => Holds("Choice", clr => clr.IsDefined(typeof(global::app.Attributes.ChoiceAttribute), inherit: false));

    [Test] public Task TheActionList_HoldsEveryActionClass()
        => Holds("Action", clr => !clr.IsAbstract && typeof(global::app.module.ICodeGenerated).IsAssignableFrom(clr)
            && System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.module.ActionAttribute>(clr) != null);

    [Test] public Task TheSerializerList_HoldsEveryClassInASerializerFolder()
        => Holds("Serializer", clr => clr.Namespace?.EndsWith(".serializer", System.StringComparison.Ordinal) == true);

    [Test] public Task ThePairList_HoldsEveryClassInAFormatFolder()
        => Holds("Pair", clr => clr.Namespace?.EndsWith(".format", System.StringComparison.Ordinal) == true);
}
