using System.Reflection;

namespace PLang.Tests.App.CompareRedesign;

// Scalar wrappers are deeply immutable — the precondition that makes wrapper
// SHARING safe (ShallowClone aliases the wrapper today; the born-typed store
// seam's instance cache will widen that to program-wide singletons, e.g. one
// bool.True for every `true` in the program). One writable field on a wrapper
// and a write through %a% appears in %b% — value corruption at a distance.
// Everything per-occurrence (name, type, kind, properties, signature) lives on
// the Data box, which is never shared.
//
// Scope: the scalar wrappers only. `dict`/`list` are deliberately absent —
// mutable containers with their own copy-on-write story. binary's byte[]
// CONTENTS are out of scope too: interior mutation is the collections
// copy-on-write concern, not wrapper shape.
public class WrapperImmutabilityTests
{
    private static readonly System.Type[] Wrappers =
    {
        typeof(global::app.type.text.@this),
        typeof(global::app.type.@bool.@this),
        typeof(global::app.type.number.@this),
        typeof(global::app.type.binary.@this),
        typeof(global::app.type.date.@this),
        typeof(global::app.type.datetime.@this),
        typeof(global::app.type.time.@this),
        typeof(global::app.type.duration.@this),
        typeof(global::app.type.@null.@this),
    };

    private static IEnumerable<(System.Type Owner, FieldInfo Field)> InstanceFields(System.Type wrapper)
    {
        // DeclaredOnly per level, walking below object — inherited mutable
        // state is just as shared as declared.
        for (var t = wrapper; t != null && t != typeof(object); t = t.BaseType)
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                yield return (t, f);
    }

    // Named exemptions — the load-once slot (`_raw`: set on load, nulled on
    // parse, owned by the type's own load path) and the set-once chain stamp
    // (`_prior`: creation history, appended once by the narrowing type, never
    // rewritten). Both disciplines live in the owning class; everything else
    // stays locked.
    private static readonly string[] ExemptFields = { "_raw", "_prior" };

    [Test]
    public async Task EveryInstanceField_IncludingInherited_IsReadonly()
    {
        var offenders = new List<string>();
        foreach (var wrapper in Wrappers)
            foreach (var (owner, field) in InstanceFields(wrapper))
                if (!field.IsInitOnly && !ExemptFields.Contains(field.Name))
                    offenders.Add($"{wrapper.FullName}: {owner.Name}.{field.Name}");
        await Assert.That(offenders).IsEmpty()
            .Because("a writable wrapper field makes shared instances corruptible at a distance");
    }

    [Test]
    public async Task NoInstanceProperty_HasASetter()
    {
        // init-only setters are creation-time stamps (kind, template) — safe.
        // A post-construction setter is the corruption vector.
        var offenders = new List<string>();
        foreach (var wrapper in Wrappers)
            for (var t = wrapper; t != null && t != typeof(object); t = t.BaseType)
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (p.SetMethod != null && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
                            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
                        offenders.Add($"{wrapper.FullName}: {t.Name}.{p.Name}");
        await Assert.That(offenders).IsEmpty()
            .Because("a settable wrapper property makes shared instances corruptible at a distance");
    }

    [Test]
    public async Task EveryWrapper_IsSealed()
    {
        // A subclass could add mutable state and flow through the same shared slots.
        var offenders = Wrappers.Where(w => !w.IsSealed).Select(w => w.FullName).ToList();
        await Assert.That(offenders).IsEmpty();
    }
}
