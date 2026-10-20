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

    // The data the generator writes as code must say what the classes say.

    private static T Data<T>(string name)
        => (T)Plang.GetType("PLang.Generated.Written")!
            .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;

    private static string Said(global::app.Attributes.FormatAttribute f)
        => $"{f.Name}|{f.Mime}|{string.Join(",", f.Extension)}|{f.Compressible}|{f.Text}";

    [Test] public async Task TheFormatData_IsEachClassesDeclarations()
    {
        var written = Data<(System.Type Class, global::app.Attributes.FormatAttribute[] Format)[]>("FormatData")
            .ToDictionary(row => row.Class, row => row.Format.Select(Said).ToList());
        foreach (var clr in Written().Where(clr => clr.IsDefined(typeof(global::app.Attributes.FormatAttribute), inherit: false)))
        {
            var declared = System.Reflection.CustomAttributeExtensions.GetCustomAttributes<global::app.Attributes.FormatAttribute>(clr, inherit: false).Select(Said).ToList();
            await Assert.That(written.TryGetValue(clr, out var listed) ? listed : null).IsEquivalentTo(declared)
                .Because($"{clr.FullName}'s formats as the generator wrote them");
        }
    }

    [Test] public async Task TheActionData_IsEachActionsAttribute()
    {
        var written = Data<(System.Type Class, global::app.module.ActionAttribute Action)[]>("ActionData")
            .ToDictionary(row => row.Class, row => $"{row.Action.Name}|{row.Action.Cacheable}");
        foreach (var clr in Written())
            if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.module.ActionAttribute>(clr) is { } action)
                await Assert.That(written.GetValueOrDefault(clr)).IsEqualTo($"{action.Name}|{action.Cacheable}")
                    .Because($"{clr.FullName}'s [Action] as the generator wrote it");
    }

    [Test] public async Task EachTypesStatics_AreWhatTheClassDeclares()
    {
        // the facts read through the generator's code; the same class read by reflection must answer the same
        foreach (var type in new global::app.assembly.@this(Plang).type)
        {
            var read = new global::app.assembly.@this.Type(type.Name, type.Class);
            await Assert.That(type.Alias).IsEquivalentTo(read.Alias).Because($"{type.Name}'s aliases");
            static string Shapes(System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> owned)
                => string.Join(",", owned.Select(o => $"{o.Clr.FullName}/{o.Kind}/{o.Assignable}"));
            await Assert.That(Shapes(type.Owned)).IsEqualTo(Shapes(read.Owned)).Because($"{type.Name}'s owned shapes");
            await Assert.That(type.Internal).IsEqualTo(read.Internal).Because($"{type.Name}'s Internal");
            static string Offered(System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>? offer)
                => offer == null ? "none" : string.Join(",", offer.Select(o => o.GetType().FullName));
            await Assert.That(Offered(type.Offer)).IsEqualTo(Offered(read.Offer)).Because($"{type.Name}'s offer");
        }
    }
}
